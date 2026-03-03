using MQTTnet;

class Program
{
    // === TEST CONFIGURATION ===
    const string CountryCode = "DE";
    const string OrgCode = "MVG";
    const string ItcsId = "ITCS001";
    const string TestVehicleId = "de:mvg:5812";
    const string TestDriverId = "de:mvg:abc";

    static async Task Main(string[] args)
    {
        var factory = new MqttClientFactory();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("localhost", 1883)
            .WithClientId($"test-drive-{Guid.NewGuid()}")
            .WithCleanSession(false) // Drive: cleanSession=false to not miss live notifications
            .Build();

        var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += e =>
        {
            var payload = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;

            Console.WriteLine("✅ RESPONSE RECEIVED:");
            Console.WriteLine($"📩 Topic: {e.ApplicationMessage.Topic}");
            Console.WriteLine(payload);
            Console.WriteLine();

            return Task.CompletedTask;
        };

        client.ConnectedAsync += e =>
        {
            Console.WriteLine("✅ Connected to broker");
            return Task.CompletedTask;
        };

        client.DisconnectedAsync += e =>
        {
            Console.WriteLine($"❌ Disconnected: {e.Reason}");
            return Task.CompletedTask;
        };

        Console.WriteLine("Connecting to broker...");
        await client.ConnectAsync(options, CancellationToken.None);

        // --- SUBSCRIPTIONS (Drive subscribes to VehicleInbox responses) ---
        var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
            // Broad subscription to catch all VehicleInbox responses
            .WithTopicFilter(f => f
                .WithTopic($"IoM/1.0/DataVersion/+/Inbox/VehicleInbox/Country/{CountryCode}/+/Organisation/{OrgCode}/+/VehicleId/+/#")
                .WithAtLeastOnceQoS())
            // LiveAnnouncement (Dispo -> Drive, subscribe only for Drive)
            .WithTopicFilter(f => f
                .WithTopic($"IoM/1.0/DataVersion/+/Inbox/ItcsInbox/Country/{CountryCode}/+/Organisation/{OrgCode}/+/ItcsId/{ItcsId}/LiveAnnouncementData")
                .WithAtLeastOnceQoS())
            // Notification (Dispo -> Drive, subscribe only for Drive)
            .WithTopicFilter(f => f
                .WithTopic($"IoM/1.0/DataVersion/+/Inbox/ItcsInbox/Country/{CountryCode}/+/Organisation/{OrgCode}/+/ItcsId/{ItcsId}/NotificationData")
                .WithAtLeastOnceQoS())
            .Build();

        await client.SubscribeAsync(subscribeOptions, CancellationToken.None);
        Console.WriteLine("✅ Subscribed to VehicleInbox response topics, LiveAnnouncement, and Notification");

        // --- TEST FLOW ---
        Console.WriteLine("\n🚀 STARTING TESTS...\n");

        await SendTechnicalLogOnRequest(client);
        await Task.Delay(1000);

        await SendTechnicalLogOffRequest(client);
        await Task.Delay(1000);

        await SendLogOnRequest(client);
        await Task.Delay(1000);

        await SendLogOffRequest(client);
        await Task.Delay(1000);

        await SendOperationalLogOnRequest(client);
        await Task.Delay(1000);

        await SendOperationalLogOffRequest(client);
        await Task.Delay(1000);

        await SendPredefinedMessageRequest(client);
        await Task.Delay(1000);

        await SendGnssPhysicalPositionRequest(client);
        await Task.Delay(1000);

        await SendDistressCallRequest(client);
        await Task.Delay(1000);

        // NOTE: LiveAnnouncement and Notification are PUBLISH-ONLY from Dispo.
        // The test client (Drive) subscribes to them but cannot trigger them.
        Console.WriteLine("ℹ️  LiveAnnouncement & Notification are Dispo-published — test client can only receive them.");

        Console.WriteLine("\n👂 Listening for responses (Press Ctrl+C to quit)...\n");
        await Task.Delay(-1);
    }

    // ---------------- SHARED HELPERS ----------------

    /// <summary>
    /// Builds the ItcsInbox publish topic for Drive -> Dispo request messages.
    /// Uses the MessageId from the XML payload as the CorrelationId in the topic.
    /// </summary>
    static string BuildItcsInboxTopic(string messageId, string dataSuffix)
    {
        return $"IoM/1.0/DataVersion/1.0/Inbox/ItcsInbox/Country/{CountryCode}/any/Organisation/{OrgCode}/any/ItcsId/{ItcsId}/CorrelationId/{messageId}/{dataSuffix}";
    }

    static async Task PublishAsync(IMqttClient client, string topic, string payload, int qos = 1, bool retain = false)
    {
        var qosLevel = qos switch
        {
            0 => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
            2 => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
            _ => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
        };

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(qosLevel)
            .WithRetainFlag(retain)
            .Build();

        await client.PublishAsync(message, CancellationToken.None);
    }

    // ---------------- TECHNICAL LOGON / LOGOFF ----------------

    static async Task SendTechnicalLogOnRequest(IMqttClient client)
    {
        var messageId = Guid.NewGuid().ToString();
        var topic = BuildItcsInboxTopic(messageId, "TechnicalVehicleLogOnRequestData");
        await PublishAsync(client, topic, BuildTechnicalLogOnXml(messageId, TestVehicleId, "obu-123", "2025-08-14.1"), qos: 1);
        Console.WriteLine($"✅ Technical LogOn Request Sent (MessageId/CorrelationId: {messageId})");
    }

    static async Task SendTechnicalLogOffRequest(IMqttClient client)
    {
        var messageId = Guid.NewGuid().ToString();
        var topic = BuildItcsInboxTopic(messageId, "TechnicalVehicleLogOffRequestData");
        await PublishAsync(client, topic, BuildTechnicalLogOffXml(messageId, TestVehicleId), qos: 1);
        Console.WriteLine($"✅ Technical LogOff Request Sent (MessageId/CorrelationId: {messageId})");
    }

    // ---------------- DRIVER LOGON / LOGOFF ----------------

    static async Task SendLogOnRequest(IMqttClient client)
    {
        var messageId = Guid.NewGuid().ToString();
        var topic = BuildItcsInboxTopic(messageId, "DriverVehicleLogOnRequestData");
        await PublishAsync(client, topic, BuildLogOnXml(messageId, TestVehicleId, TestDriverId), qos: 1);
        Console.WriteLine($"✅ Driver LogOn Request Sent (MessageId/CorrelationId: {messageId})");
    }

    static async Task SendLogOffRequest(IMqttClient client)
    {
        var messageId = Guid.NewGuid().ToString();
        var topic = BuildItcsInboxTopic(messageId, "DriverVehicleLogOffRequestData");
        await PublishAsync(client, topic, BuildLogOffXml(messageId, TestVehicleId, TestDriverId), qos: 1);
        Console.WriteLine($"✅ Driver LogOff Request Sent (MessageId/CorrelationId: {messageId})");
    }

    // ---------------- OPERATIONAL LOGON / LOGOFF ----------------

    static async Task SendOperationalLogOnRequest(IMqttClient client)
    {
        var messageId = Guid.NewGuid().ToString();
        var topic = BuildItcsInboxTopic(messageId, "OperationalVehicleLogOnRequestData");
        await PublishAsync(client, topic, BuildOperationalLogOnXml(
            messageId, "de:mvg:1234", "vehicleJourney:12345", "operatingDay:67890", "block:54321", "de:mvg:12345"), qos: 1);
        Console.WriteLine($"✅ Operational LogOn Request Sent (MessageId/CorrelationId: {messageId})");
    }

    static async Task SendOperationalLogOffRequest(IMqttClient client)
    {
        var messageId = Guid.NewGuid().ToString();
        var topic = BuildItcsInboxTopic(messageId, "OperationalVehicleLogOffRequestData");
        await PublishAsync(client, topic, BuildOperationalLogOffXml(
            messageId, "de:mvg:1234", "vehicleJourney:12345", "operatingDay:67890", "block:54321", "de:mvg:12345"), qos: 1);
        Console.WriteLine($"✅ Operational LogOff Request Sent (MessageId/CorrelationId: {messageId})");
    }

    // ---------------- PREDEFINED MESSAGE ----------------

    static async Task SendPredefinedMessageRequest(IMqttClient client)
    {
        var messageId = Guid.NewGuid().ToString();
        var topic = BuildItcsInboxTopic(messageId, "PredefinedMessageRequestData");
        await PublishAsync(client, topic, BuildPredefinedMessageXml(messageId, "10", "Traffic Jam - 10 min delay"), qos: 1);
        Console.WriteLine($"✅ Predefined Message Request Sent (MessageId/CorrelationId: {messageId})");
    }

    // ---------------- GNSS (QoS 0, Retain=true) ----------------

    static async Task SendGnssPhysicalPositionRequest(IMqttClient client)
    {
        var topic = $"IoM/1.0/DataVersion/1.0/Country/{CountryCode}/any/Organisation/{OrgCode}/any/Vehicle/{TestVehicleId}/any/PhysicalPosition/GnssPhysicalPositionData";
        await PublishAsync(client, topic, BuildGnssPhysicalPositionXml(), qos: 0, retain: true);
        Console.WriteLine("✅ GnssPhysicalPosition Sent (QoS 0, Retain=true)");
    }

    // ---------------- DISTRESS CALL ----------------

    static async Task SendDistressCallRequest(IMqttClient client)
    {
        var messageId = Guid.NewGuid().ToString();
        var topic = BuildItcsInboxTopic(messageId, "DistressCallRequestData");
        await PublishAsync(client, topic, BuildDistressCallRequestXml(messageId), qos: 1);
        Console.WriteLine($"✅ Distress Call Request Sent (MessageId/CorrelationId: {messageId})");
    }

    // ---------------- XML BUILDERS ----------------
    // MessageId is now generated ONCE and passed in — same value goes into both topic and payload

    static string BuildTechnicalLogOnXml(string messageId, string vehicleRef, string obuId, string baseVersion) => $"""
<TechnicalVehicleLogOnRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{messageId}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" nameOfRefClass="Vehicle" version="1.0" />
    <OnboardUnitId>{obuId}</OnboardUnitId>
    <DataVersion>{baseVersion}</DataVersion>
</TechnicalVehicleLogOnRequestStructure>
""";

    static string BuildTechnicalLogOffXml(string messageId, string vehicleRef) => $"""
<TechnicalVehicleLogOffRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{messageId}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0" />
</TechnicalVehicleLogOffRequestStructure>
""";

    static string BuildLogOnXml(string messageId, string vehicleRef, string driverRef) => $"""
<DriverVehicleLogOnRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{messageId}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <netex:DriverRef ref="{driverRef}" version="1.0"/>
</DriverVehicleLogOnRequestStructure>
""";

    static string BuildLogOffXml(string messageId, string vehicleRef, string driverRef) => $"""
<DriverVehicleLogOffRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{messageId}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <netex:DriverRef ref="{driverRef}" version="1.0"/>
    <Extensions/>
</DriverVehicleLogOffRequestStructure>
""";

    static string BuildOperationalLogOnXml(string messageId, string vehicleRef, string vehicleJourneyRef, string operatingDayRef, string blockRef, string journeyPatternRef) => $"""
<OperationalVehicleLogOnRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{messageId}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <DatedJourneyRef>
        <VehicleJourneyRef ref="{vehicleJourneyRef}" nameOfRefClass="VehicleJourney" version="1.0"/>
        <OperatingDayRef ref="{operatingDayRef}" nameOfRefClass="OperatingDay" version="1.0"/>
        <BlockRef ref="{blockRef}" nameOfRefClass="Block" version="1.0"/>
    </DatedJourneyRef>
    <netex:JourneyPatternRef ref="{journeyPatternRef}" nameOfRefClass="JourneyPattern" version="1.0"/>
    <Extensions/>
</OperationalVehicleLogOnRequestStructure>
""";

    static string BuildOperationalLogOffXml(string messageId, string vehicleRef, string vehicleJourneyRef, string operatingDayRef, string blockRef, string journeyPatternRef) => $"""
<OperationalVehicleLogOffRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{messageId}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <DatedJourneyRef>
        <VehicleJourneyRef ref="{vehicleJourneyRef}" nameOfRefClass="VehicleJourney" version="1.0"/>
        <OperatingDayRef ref="{operatingDayRef}" nameOfRefClass="OperatingDay" version="1.0"/>
        <BlockRef ref="{blockRef}" nameOfRefClass="Block" version="1.0"/>
    </DatedJourneyRef>
    <netex:JourneyPatternRef ref="{journeyPatternRef}" nameOfRefClass="JourneyPattern" version="1.0"/>
    <Extensions/>
</OperationalVehicleLogOffRequestStructure>
""";

    static string BuildPredefinedMessageXml(string messageId, string messageCode, string description) => $"""
<PredefinedMessageRequest xmlns:xs="http://www.w3.org/2001/XMLSchema" xs:version="1.0" xs:dateTime="{UtcNow()}">
    <MessageId>{messageId}</MessageId>
    <MessageData description="{description}"/>
</PredefinedMessageRequest>
""";

    static string BuildGnssPhysicalPositionXml() => $"""
<GnssPhysicalPositionDataStructure xmlns:gml="http://www.opengis.net/gml/3.2">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <TimestampOfMeasurement>{UtcNow()}</TimestampOfMeasurement>
    <PublisherId>publisher-001</PublisherId>
    <GnssPhysicalPosition>
        <WGS84PhysicalPosition id="loc1" srsName="EPSG:4326">
            <Longitude>2.356</Longitude>
            <Latitude>56.356</Latitude>
            <Altitude>100</Altitude>
            <gml:pos>2.356 56.356 100</gml:pos>
            <Precision>10</Precision>
        </WGS84PhysicalPosition>
        <NumberOfVisibleSatellites>8</NumberOfVisibleSatellites>
        <CompassBearing>90</CompassBearing>
        <Velocity>12.5</Velocity>
    </GnssPhysicalPosition>
    <Extensions/>
</GnssPhysicalPositionDataStructure>
""";

    static string BuildDistressCallRequestXml(string messageId) => $"""
<DistressCallRequest xmlns:xs="http://www.w3.org/2001/XMLSchema" xs:version="1.0" xs:dateTime="{UtcNow()}">
    <MessageId>{messageId}</MessageId>
</DistressCallRequest>
""";

    static string UtcNow() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}
