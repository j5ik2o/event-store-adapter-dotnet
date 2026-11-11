using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using EventStoreAdapterDotNet;
using EventStoreAdapterDotNet.impl;

// Spike for j5ik2o/event-store-adapter-dotnet#84. Runs the DynamoDB-related cases of the
// conformance data v1.4.0 against the 2023 WIP API. Not a production runner.
return args switch
{
    ["run", var dataDir, var endpoint, .. var rest] => await Runner.Run(dataDir, endpoint, rest),
    ["probe", var endpoint] => await Runner.Probe(endpoint),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: run <conformance-dir> <endpoint> [--lenient-create] [--out <file>] | probe <endpoint>");
    return 2;
}

enum Status { Passed, Failed, Unrepresentable, NotApplicable }

record CaseResult(string File, string Id, Status Status, string Reason, int? FailedStep = null);

sealed class Aid : IAggregateId
{
    public string TypeName { get; init; } = "";
    public string Value { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserString { get; init; }
    public string AsString() => UserString ?? $"{TypeName}-{Value}";
}

sealed class Event : IEvent<Aid>
{
    public string Id { get; init; } = "";
    public Aid AggregateId { get; init; } = new();
    public long SequenceNumber { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public bool IsCreated => SequenceNumber == 1;
    public string Manifest { get; init; } = "";
    public JsonNode? Payload { get; init; }
}

sealed class Agg : IAggregate<Agg, Aid>
{
    public Aid Id { get; init; } = new();
    public long SequenceNumber { get; init; }
    public long Version { get; init; }
    public string Manifest { get; init; } = "";
    public JsonNode? State { get; init; }
    public Agg WithVersion(long version) =>
        new() { Id = Id, SequenceNumber = SequenceNumber, Version = version, Manifest = Manifest, State = State };
}

record Fixtures(Dictionary<string, Event> Events, Dictionary<string, Agg> Snapshots);

sealed class StepFailure(string reason) : Exception(reason);

sealed class Session(AmazonDynamoDBClient client, string journal, string snapshot, bool lenientCreate)
{
    public EventStoreForDynamoDb<Aid, Agg, Event> Store { get; } = new(client, journal, snapshot,
        "journal-aid-index", "snapshot-aid-index", 32, new DefaultKeyResolver<Aid>(),
        new DefaultEventSerializer<Aid, Event>(), new DefaultSnapshotSerializer<Aid, Agg>());

    public string Journal => journal;

    // The WIP API makes the caller pass the snapshot version; a careful caller reads it first.
    async Task<long> CurrentVersion(Aid aid) => (await Store.GetLatestSnapshotById(aid))?.Version ?? 0;

    public async Task PersistEvent(Event e)
    {
        if (lenientCreate && e.IsCreated)
        {
            await Store.PersistEventAndSnapshot(e, new Agg { Id = e.AggregateId, SequenceNumber = 1, Version = 1 });
            return;
        }
        await Store.PersistEvent(e, await CurrentVersion(e.AggregateId));
    }

    public async Task PersistEventAndSnapshot(Event e, Agg s)
    {
        var withId = new Agg
        {
            Id = e.AggregateId, SequenceNumber = s.SequenceNumber, Manifest = s.Manifest, State = s.State,
            Version = e.IsCreated ? 1 : await CurrentVersion(e.AggregateId),
        };
        await Store.PersistEventAndSnapshot(e, withId);
    }
}

static class Runner
{
    static readonly string[] ScenarioFiles =
    [
        "scenarios/core/write-read.json", "scenarios/core/retention-errors.json",
        "dynamodb/configuration.json", "dynamodb/write-errors.json", "dynamodb/read.json",
        "dynamodb/retention.json", "dynamodb/single-aggregate-requests.json", "dynamodb/item-shapes.json",
    ];

    static readonly string[] ValueFiles = ["values/aid.json", "values/seq-nr.json", "values/occurred-at.json"];

    static AmazonDynamoDBClient Client(string endpoint) =>
        new(new BasicAWSCredentials("x", "x"),
            new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1", MaxErrorRetry = 0 });

    public static async Task<int> Run(string dataDir, string endpoint, string[] rest)
    {
        var lenient = rest.Contains("--lenient-create");
        var outIdx = Array.IndexOf(rest, "--out");
        var outPath = outIdx >= 0 ? rest[outIdx + 1] : null;
        var manifest = VerifyManifest(dataDir);
        Console.WriteLine($"data {manifest.version}, manifest {(manifest.mismatches.Count == 0 ? "ok" : "MISMATCH " + string.Join(",", manifest.mismatches))}, mode {(lenient ? "lenient-create" : "strict")}");
        if (manifest.mismatches.Count > 0) return 1;

        using var client = Client(endpoint);
        var prefix = $"spike84-{Guid.NewGuid():N}"[..16];
        var results = new List<CaseResult>();
        var n = 0;
        foreach (var file in ValueFiles.Concat(ScenarioFiles).Append("dynamodb/layout.json"))
        {
            var doc = JsonNode.Parse(File.ReadAllText(Path.Combine(dataDir, file)))!.AsObject();
            foreach (var c in doc["cases"]!.AsArray().Select(x => x!.AsObject()))
            {
                if (c["backends"] is JsonArray b && !b.Any(x => x!.GetValue<string>() == "dynamodb")) continue;
                var r = await RunCase(client, $"{prefix}-{n++}", file, doc["format"]!.GetValue<string>(), c, lenient);
                results.Add(r);
                Console.WriteLine($"{r.Status,-15} {r.File} {r.Id}{(r.FailedStep is { } s ? $" step {s}" : "")} {r.Reason}");
            }
        }

        Console.WriteLine();
        foreach (var g in results.GroupBy(r => r.File))
            Console.WriteLine($"{g.Key,-42} {string.Join("  ", Enum.GetValues<Status>().Select(s => $"{s}={g.Count(r => r.Status == s)}"))}");
        Console.WriteLine($"{"TOTAL " + results.Count,-42} {string.Join("  ", Enum.GetValues<Status>().Select(s => $"{s}={results.Count(r => r.Status == s)}"))}");
        if (outPath != null)
            File.WriteAllText(outPath, JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                WriteIndented = true, Converters = { new JsonStringEnumConverter() },
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
        return 0;
    }

    static (string version, List<string> mismatches) VerifyManifest(string dataDir)
    {
        var m = JsonNode.Parse(File.ReadAllText(Path.Combine(dataDir, "manifest.json")))!;
        var bad = new List<string>();
        foreach (var f in m["files"]!.AsArray())
        {
            var path = Path.Combine(dataDir, f!["path"]!.GetValue<string>());
            var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            if (hash != f["sha256"]!.GetValue<string>()) bad.Add(f["path"]!.GetValue<string>());
        }
        return (m["version"]!.GetValue<string>(), bad);
    }

    static async Task<CaseResult> RunCase(AmazonDynamoDBClient client, string tablePrefix, string file, string format, JsonObject c, bool lenient)
    {
        var id = c["id"]!.GetValue<string>();
        CaseResult R(Status s, string reason, int? step = null) => new(file, id, s, reason, step);

        if (format == "layout")
            return R(Status.Failed, "WIP: pkey/skey (shard) layout with aid GSIs and no head table; spec needs aid/seq_nr keys and a head table (DY-16, DY-17, D-2)");
        if (format == "values")
            return await RunValue(client, tablePrefix, file, c, lenient, R);

        var store = c["store"]?.AsObject();
        if (c["seed"] != null)
            return R(Status.Failed, "WIP: seed items use the spec layout (aid keys, head, configuration items) that the WIP tables cannot hold (DY-16, DY-8)");
        if (c["initialization"] != null)
            return R(Status.Failed, "WIP: store construction does no configuration check or I/O (DY-8, S-1)");
        if (store?["retention_count"] is not null || store?["retention_mode"]?.GetValue<string>() == "ttl")
            return R(Status.Failed, "WIP: no snapshot retention setting (keepSnapshot is a TODO; S-1, S-2)");
        if (c["faults"] is JsonArray { Count: > 0 })
            return R(Status.Unrepresentable, "runner: fault injection not implemented (the WIP takes a concrete AmazonDynamoDBClient)");

        ApplyGenerators(c);
        var fx = ReadFixtures(c);
        var session = await NewSession(client, tablePrefix, lenient);
        var steps = c["steps"]!.AsArray();
        var observed = false;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i]!.AsObject();
            observed |= step["observe"] != null;
            var failure = await RunStep(session, step, fx);
            if (failure != null) return R(Status.Failed, failure, i + 1);
        }
        return observed
            ? R(Status.Unrepresentable, "runner: every expect matched, but observe (requests, items, history) is not implemented")
            : R(Status.Passed, "");
    }

    static async Task<string?> RunStep(Session s, JsonObject step, Fixtures fx)
    {
        var op = step["op"]!.GetValue<string>();
        var args = step["arguments"]!.AsObject();
        var expect = step["expect"]!.AsObject();
        object? actual = null;
        Exception? error = null;
        try
        {
            switch (op)
            {
                case "persistEvent":
                    await s.PersistEvent(fx.Events[args["event"]!.GetValue<string>()]);
                    break;
                case "persistEventAndSnapshot":
                    await s.PersistEventAndSnapshot(fx.Events[args["event"]!.GetValue<string>()], fx.Snapshots[args["snapshot"]!.GetValue<string>()]);
                    break;
                case "getLatestSnapshotById":
                    actual = await s.Store.GetLatestSnapshotById(ReadAid(args["aggregate_id"]!));
                    break;
                case "getEventsByIdSinceSeqNr":
                    actual = await s.Store.GetEventsByIdSinceSequenceNumber(ReadAid(args["aggregate_id"]!), ReadLong(args["seq_nr"]!));
                    break;
                default:
                    return $"runner: unknown op {op}";
            }
        }
        catch (Exception e)
        {
            error = e;
        }

        if (expect["error"] is JsonObject expectedError)
        {
            var category = expectedError["category"]!.GetValue<string>();
            return error == null
                ? $"{op}: no error returned (expected {category})"
                : $"{op}: {error.GetType().Name} cannot be classified as {category} (E-1)";
        }
        if (error != null) return $"{op}: {error.GetType().Name}: {Oneline(error.Message)}";

        switch (expect["result"]!.GetValue<string>())
        {
            case "success":
                return null;
            case "none":
                return actual == null ? null : $"{op}: expected none, got a snapshot";
            case "snapshot":
                return $"{op}: the WIP returns only the aggregate, not (snapshot envelope, head seq_nr); got {(actual == null ? "null" : "an aggregate")} (R-1, R-2)";
            case "events":
                return CompareEvents((List<Event>)actual!, expect["events"]!.AsArray(), fx, op);
            default:
                return $"runner: unknown expect {expect["result"]}";
        }
    }

    static string? CompareEvents(List<Event> actual, JsonArray expectedRefs, Fixtures fx, string op)
    {
        var expected = expectedRefs.Select(r => fx.Events[r!.GetValue<string>()]).ToList();
        if (actual.Count != expected.Count) return $"{op}: {actual.Count} events, expected {expected.Count}";
        for (var i = 0; i < expected.Count; i++)
        {
            var (a, e) = (actual[i], expected[i]);
            if (a.AggregateId.AsString() != e.AggregateId.AsString()) return $"{op}: event {i} aggregate_id differs";
            if (a.SequenceNumber != e.SequenceNumber) return $"{op}: event {i} seq_nr {a.SequenceNumber} != {e.SequenceNumber}";
            if (a.OccurredAt != e.OccurredAt) return $"{op}: event {i} occurred_at {a.OccurredAt:o} != {e.OccurredAt:o}";
            if (a.Manifest != e.Manifest) return $"{op}: event {i} manifest differs";
            if (!JsonEq(a.Payload, e.Payload)) return $"{op}: event {i} payload differs";
        }
        return null;
    }

    static async Task<CaseResult> RunValue(AmazonDynamoDBClient client, string tablePrefix, string file, JsonObject c, bool lenient, Func<Status, string, int?, CaseResult> R)
    {
        var input = c["input"]!.AsObject();
        var expect = c["expect"]!.AsObject();
        var expectsError = expect["error"] != null;
        var session = await NewSession(client, tablePrefix, lenient);
        switch (c["operation"]!.GetValue<string>())
        {
            case "buildAid":
            {
                var raw = input["aggregate_id"]!;
                var aid = new Aid
                {
                    TypeName = raw["type_name"]!.GetValue<string>(), Value = raw["value"]!.GetValue<string>(),
                    UserString = input["user_string"]?.GetValue<string>(),
                };
                var e = new Event { AggregateId = aid, SequenceNumber = 1, OccurredAt = DateTimeOffset.UnixEpoch, Payload = new JsonObject() };
                Exception? error = null;
                try { await session.Store.PersistEventAndSnapshot(e, new Agg { Id = aid, SequenceNumber = 1, Version = 1 }); }
                catch (Exception x) { error = x; }
                var stored = (await client.ScanAsync(new ScanRequest { TableName = session.Journal })).Items
                    .Select(item => item["aid"].S).ToList();
                if (expectsError)
                    return R(Status.Failed, error == null
                        ? $"no error returned; journal holds aid of {Encoding.UTF8.GetByteCount(stored.FirstOrDefault() ?? "")} bytes (expected {expect["error"]!["rule"]})"
                        : $"{error.GetType().Name} cannot be classified (E-1)", null);
                var want = expect["value"]!.GetValue<string>();
                return stored.SequenceEqual([want])
                    ? R(Status.Passed, "", null)
                    : R(Status.Failed, $"stored aid is the user's AsString() '{Trim(stored.FirstOrDefault())}', expected '{Trim(want)}' (T-1)", null);
            }
            case "validateSeqNr":
            {
                var seq = ReadLong(input["seq_nr"]!);
                var e = new Event { AggregateId = new Aid { TypeName = "ConformanceSeq", Value = c["id"]!.GetValue<string>() }, SequenceNumber = seq, Payload = new JsonObject() };
                Exception? error = null;
                try { await session.Store.PersistEventAndSnapshot(e, new Agg { Id = e.AggregateId, SequenceNumber = seq, Version = 1 }); }
                catch (Exception x) { error = x; }
                if (expectsError)
                    return R(Status.Failed, error == null ? "no error returned: the WIP has no seq_nr range check (T-9, W-6)" : $"{error.GetType().Name} cannot be classified (E-1)", null);
                return error == null
                    ? R(Status.Passed, "vacuous: the WIP has no range check to reject the value", null)
                    : R(Status.Failed, $"{error.GetType().Name}: {Oneline(error.Message)}", null);
            }
            case "validateOccurredAt":
            {
                if (c["representation"]?["time_precision"]?.GetValue<string>() == "nanoseconds")
                    return R(Status.Unrepresentable, "DateTimeOffset has 100 ns ticks; the data has nanosecond and millisecond boundaries but no 100 ns ones", null);
                var aid = new Aid { TypeName = "ConformanceTime", Value = c["id"]!.GetValue<string>() };
                var target = (int)ReadLong(input["event_seq_nr"]!);
                var filler = ParseTime("1970-01-01T00:00:00.123000000Z");
                try
                {
                    for (var i = 1; i <= target; i++)
                        await session.PersistEvent(new Event
                        {
                            AggregateId = aid, SequenceNumber = i, Payload = new JsonObject(),
                            OccurredAt = i == target ? ParseTime(input["iso8601"]!.GetValue<string>()) : filler,
                        });
                }
                catch (Exception x)
                {
                    return R(Status.Failed, expectsError ? $"{x.GetType().Name} cannot be classified (E-1)" : $"persistEvent: {x.GetType().Name}: {Oneline(x.Message)}", null);
                }
                if (expectsError) return R(Status.Failed, "no error returned: the WIP has no occurred_at range check (T-13)", null);
                try
                {
                    var read = await session.Store.GetEventsByIdSinceSequenceNumber(aid, 1);
                    var want = ParseTime(input["iso8601"]!.GetValue<string>());
                    return read.Count == 1 && read[0].OccurredAt == want
                        ? R(Status.Passed, "", null)
                        : R(Status.Failed, $"read {read.Count} events, occurred_at {read.FirstOrDefault()?.OccurredAt:o}, expected {want:o} (T-3)", null);
                }
                catch (Exception x)
                {
                    return R(Status.Failed, $"getEventsByIdSinceSeqNr: {x.GetType().Name}: {Oneline(x.Message)}", null);
                }
            }
            default:
                return R(Status.NotApplicable, "not a DynamoDB value operation", null);
        }
    }

    static async Task<Session> NewSession(AmazonDynamoDBClient client, string prefix, bool lenient)
    {
        var (journal, snapshot) = ($"{prefix}-journal", $"{prefix}-snapshot");
        await CreateWipTable(client, journal, "journal-aid-index");
        await CreateWipTable(client, snapshot, "snapshot-aid-index");
        return new Session(client, journal, snapshot, lenient);
    }

    // The WIP's own layout: pkey/skey strings plus an (aid, seq_nr) GSI read by both queries.
    static Task CreateWipTable(AmazonDynamoDBClient client, string name, string index) =>
        client.CreateTableAsync(new CreateTableRequest
        {
            TableName = name,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions =
            [
                new() { AttributeName = "pkey", AttributeType = ScalarAttributeType.S },
                new() { AttributeName = "skey", AttributeType = ScalarAttributeType.S },
                new() { AttributeName = "aid", AttributeType = ScalarAttributeType.S },
                new() { AttributeName = "seq_nr", AttributeType = ScalarAttributeType.N },
            ],
            KeySchema = [new() { AttributeName = "pkey", KeyType = KeyType.HASH }, new() { AttributeName = "skey", KeyType = KeyType.RANGE }],
            GlobalSecondaryIndexes =
            [
                new()
                {
                    IndexName = index,
                    KeySchema = [new() { AttributeName = "aid", KeyType = KeyType.HASH }, new() { AttributeName = "seq_nr", KeyType = KeyType.RANGE }],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                },
            ],
        });

    static Fixtures ReadFixtures(JsonObject c)
    {
        var f = c["fixtures"]!.AsObject();
        var events = f["events"]?.AsObject().ToDictionary(kv => kv.Key, kv => new Event
        {
            AggregateId = ReadAid(kv.Value!["aggregate_id"]!),
            SequenceNumber = ReadLong(kv.Value["seq_nr"]!),
            OccurredAt = ParseTime(kv.Value["occurred_at"]!.GetValue<string>()),
            Manifest = kv.Value["manifest"]?.GetValue<string>() ?? "",
            Payload = kv.Value["payload"]?.DeepClone(),
        }) ?? [];
        var snapshots = f["snapshots"]?.AsObject().ToDictionary(kv => kv.Key, kv => new Agg
        {
            SequenceNumber = ReadLong(kv.Value!["seq_nr"]!),
            Manifest = kv.Value["manifest"]?.GetValue<string>() ?? "",
            State = kv.Value["aggregate"]?.DeepClone(),
        }) ?? [];
        return new Fixtures(events, snapshots);
    }

    static void ApplyGenerators(JsonObject c)
    {
        if (c["generators"] is not JsonArray gens) return;
        foreach (var g in gens)
        {
            var ch = g!["character"]!.GetValue<string>();
            var text = string.Concat(Enumerable.Repeat(ch, g["byte_length"]!.GetValue<int>() / Encoding.UTF8.GetByteCount(ch)));
            var parts = g["target"]!.GetValue<string>().Split('/').Skip(1).Select(p => p.Replace("~1", "/").Replace("~0", "~")).ToArray();
            JsonNode node = c;
            foreach (var p in parts[..^1]) node = node is JsonArray a ? a[int.Parse(p)]! : node[p]!;
            if (node is JsonArray arr) arr[int.Parse(parts[^1])] = text;
            else node[parts[^1]] = text;
        }
    }

    static Aid ReadAid(JsonNode n) => new() { TypeName = n["type_name"]!.GetValue<string>(), Value = n["value"]!.GetValue<string>() };

    static long ReadLong(JsonNode n) => long.Parse(n.ToJsonString(), CultureInfo.InvariantCulture);

    static DateTimeOffset ParseTime(string iso)
    {
        var z = iso.TrimEnd('Z');
        var dot = z.IndexOf('.');
        var seconds = DateTime.ParseExact(dot < 0 ? z : z[..dot], "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var nanos = dot < 0 ? 0 : long.Parse(z[(dot + 1)..].PadRight(9, '0')[..9], CultureInfo.InvariantCulture);
        return new DateTimeOffset(seconds.Ticks + nanos / 100, TimeSpan.Zero);
    }

    static bool JsonEq(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return (a, b) switch
        {
            (JsonObject oa, JsonObject ob) => oa.Count == ob.Count && oa.All(kv => ob.TryGetPropertyValue(kv.Key, out var v) && JsonEq(kv.Value, v)),
            (JsonArray aa, JsonArray ab) => aa.Count == ab.Count && aa.Zip(ab).All(p => JsonEq(p.First, p.Second)),
            (JsonValue va, JsonValue vb) => ValueEq(va, vb),
            _ => false,
        };
    }

    static bool ValueEq(JsonValue a, JsonValue b)
    {
        var (ka, kb) = (a.GetValueKind(), b.GetValueKind());
        if (ka != kb) return false;
        if (ka == JsonValueKind.String) return a.GetValue<string>() == b.GetValue<string>();
        if (ka != JsonValueKind.Number) return true;
        return decimal.TryParse(a.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var da)
               && decimal.TryParse(b.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var db)
            ? da == db
            : a.ToJsonString() == b.ToJsonString();
    }

    static string Oneline(string s) => s.ReplaceLineEndings(" ").Length > 160 ? s.ReplaceLineEndings(" ")[..160] + "..." : s.ReplaceLineEndings(" ");

    static string Trim(string? s) => s == null ? "(none)" : s.Length > 40 ? s[..40] + "..." : s;

    // Shows three WIP defects directly: the shard key comes from string.GetHashCode (randomized
    // per process), occurred_at stores only the millisecond component, and write errors vanish.
    public static async Task<int> Probe(string endpoint)
    {
        using var client = Client(endpoint);
        var aid = new Aid { TypeName = "Order", Value = "9" };
        Console.WriteLine($"pkey for Order-9 in this process: {new DefaultKeyResolver<Aid>().ResolvePartitionKey(aid, 32)}");

        var session = await NewSession(client, $"spike84-probe-{Guid.NewGuid():N}"[..20], lenient: true);
        var e = new Event { AggregateId = aid, SequenceNumber = 1, OccurredAt = ParseTime("2026-10-05T00:00:00.123456789Z"), Payload = new JsonObject { ["x"] = 1 } };
        await session.PersistEvent(e);
        var item = (await client.ScanAsync(new ScanRequest { TableName = session.Journal })).Items.Single();
        Console.WriteLine($"journal item: aid={item["aid"].S} seq_nr={item["seq_nr"].N} occurred_at={item["occurred_at"].N} (event occurred_at {e.OccurredAt:o})");

        Exception? duplicate = null;
        try { await session.PersistEvent(e); }
        catch (Exception x) { duplicate = x; }
        Console.WriteLine($"second create of seq_nr 1: {(duplicate == null ? "returned without error" : duplicate.GetType().Name)}");

        var stale = new Event { AggregateId = aid, SequenceNumber = 2, OccurredAt = e.OccurredAt, Payload = new JsonObject() };
        Exception? staleError = null;
        try { await session.Store.PersistEvent(stale, version: 99); }
        catch (Exception x) { staleError = x; }
        var count = (await client.ScanAsync(new ScanRequest { TableName = session.Journal })).Items.Count;
        Console.WriteLine($"update with a wrong version: {(staleError == null ? "returned without error" : staleError.GetType().Name)}; journal items now {count}");
        return 0;
    }
}
