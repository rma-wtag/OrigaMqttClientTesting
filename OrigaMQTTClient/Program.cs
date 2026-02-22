using MQTTnet;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        var factory = new MqttClientFactory(); // v5: MqttClientFactory, not MqttFactory

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("localhost", 1883)
            .WithClientId($"test-client-{Guid.NewGuid()}")
            .WithCleanSession()
            .Build();

        var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += e =>
        {
            // v5: use ConvertPayloadToString() instead of PayloadSegment
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

        // --- SUBSCRIPTIONS ---
        var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic("vdv/test/login/response").WithAtLeastOnceQoS())
            .WithTopicFilter(f => f.WithTopic("vdv/test/logout/response").WithAtLeastOnceQoS())
            .WithTopicFilter(f => f.WithTopic("vdv/test/operational_login/response").WithAtLeastOnceQoS())
            .WithTopicFilter(f => f.WithTopic("vdv/test/operational_logout/response").WithAtLeastOnceQoS())
            .WithTopicFilter(f => f.WithTopic("vdv/test/predefined_message/response").WithAtLeastOnceQoS())
            .WithTopicFilter(f => f.WithTopic("vdv/test/technical_login/response").WithAtLeastOnceQoS())
            .WithTopicFilter(f => f.WithTopic("vdv/test/technical_logout/response").WithAtLeastOnceQoS())
            .Build();

        await client.SubscribeAsync(subscribeOptions, CancellationToken.None);
        Console.WriteLine("✅ Subscribed to all response topics");

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

        await SendLiveAnnouncementRequest(client);
        await Task.Delay(1000);

        await SendNotificationResponse(client);
        await Task.Delay(1000);

        await SendDistressCallRequest(client);

        Console.WriteLine("\n👂 Listening for responses (Press Ctrl+C to quit)...\n");
        await Task.Delay(-1);
    }

    // ---------------- SHARED PUBLISHER ----------------

    static async Task PublishAsync(IMqttClient client, string topic, string payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await client.PublishAsync(message, CancellationToken.None);
    }

    // ---------------- TECHNICAL LOGON / LOGOFF ----------------

    static async Task SendTechnicalLogOnRequest(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/technical_login", BuildTechnicalLogOnXml("de:mvg:5812", "obu-123", "2025-08-14.1"));
        Console.WriteLine("✅ Technical LogOn Request Sent");
    }

    static async Task SendTechnicalLogOffRequest(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/technical_logout", BuildTechnicalLogOffXml("de:mvg:5812"));
        Console.WriteLine("✅ Technical LogOff Request Sent");
    }

    // ---------------- DRIVER LOGON / LOGOFF ----------------

    static async Task SendLogOnRequest(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/login", BuildLogOnXml("de:mvg:5812", "de:mvg:abc"));
        Console.WriteLine("✅ LogOn Request Sent");
    }

    static async Task SendLogOffRequest(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/logout", BuildLogOffXml("de:mvg:5812", "de:mvg:abc"));
        Console.WriteLine("✅ LogOff Request Sent");
    }

    // ---------------- OPERATIONAL LOGON / LOGOFF ----------------

    static async Task SendOperationalLogOnRequest(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/operational_login", BuildOperationalLogOnXml(
            "de:mvg:1234", "vehicleJourney:12345", "operatingDay:67890", "block:54321", "de:mvg:12345"));
        Console.WriteLine("✅ Operational LogOn Request Sent");
    }

    static async Task SendOperationalLogOffRequest(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/operational_logout", BuildOperationalLogOffXml(
            "de:mvg:1234", "vehicleJourney:12345", "operatingDay:67890", "block:54321", "de:mvg:12345"));
        Console.WriteLine("✅ Operational LogOff Request Sent");
    }

    // ---------------- PREDEFINED MESSAGE ----------------

    static async Task SendPredefinedMessageRequest(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/predefined_message", BuildPredefinedMessageXml("10", "Traffic Jam - 10 min delay"));
        Console.WriteLine("✅ Predefined Message Request Sent");
    }

    // ---------------- GNSS ----------------

    static async Task SendGnssPhysicalPositionRequest(IMqttClient client)
    {
        var topic = "IoM/1.0/DataVersion/1.0/Country/DE/BE/Organisation/MVG/100/Vehicle/BUS/1234/PhysicalPosition/GnssPhysicalPositionData";
        await PublishAsync(client, topic, BuildGnssPhysicalPositionXml());
        Console.WriteLine("✅ GnssPhysicalPosition Request Sent");
    }

    // ---------------- LIVE ANNOUNCEMENT ----------------

    static async Task SendLiveAnnouncementRequest(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/announcement", BuildLiveAnnouncementXml("123", "https://url_to_audio_file"));
        Console.WriteLine("✅ Live Announcement Request Sent");
    }

    // ---------------- NOTIFICATION ----------------

    static async Task SendNotificationResponse(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/notification", BuildNotificationResponseXml("123", "Experiencing delay due to traffic"));
        Console.WriteLine("✅ Notification Response Sent");
    }

    // ---------------- DISTRESS CALL ----------------

    static async Task SendDistressCallRequest(IMqttClient client)
    {
        await PublishAsync(client, "vdv/test/distress", BuildDistressCallRequestXml());
        Console.WriteLine("✅ Distress Call Request Sent");
    }

    // ---------------- XML BUILDERS ----------------

    static string BuildTechnicalLogOnXml(string vehicleRef, string obuId, string baseVersion) => $"""
<TechnicalVehicleLogOnRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" nameOfRefClass="Vehicle" version="1.0" />
    <OnboardUnitId>{obuId}</OnboardUnitId>
    <BaseVersion>{baseVersion}</BaseVersion>
</TechnicalVehicleLogOnRequestStructure>
""";

    static string BuildTechnicalLogOffXml(string vehicleRef) => $"""
<TechnicalVehicleLogOffRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0" />
</TechnicalVehicleLogOffRequestStructure>
""";

    static string BuildLogOnXml(string vehicleRef, string driverRef) => $"""
<DriverVehicleLogOnRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <netex:DriverRef ref="{driverRef}" version="1.0"/>
</DriverVehicleLogOnRequestStructure>
""";

    static string BuildLogOffXml(string vehicleRef, string driverRef) => $"""
<DriverVehicleLogOffRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <netex:DriverRef ref="{driverRef}" version="1.0"/>
    <Extensions/>
</DriverVehicleLogOffRequestStructure>
""";

    static string BuildOperationalLogOnXml(string vehicleRef, string vehicleJourneyRef, string operatingDayRef, string blockRef, string journeyPatternRef) => $"""
<OperationalVehicleLogOnRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
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

    static string BuildOperationalLogOffXml(string vehicleRef, string vehicleJourneyRef, string operatingDayRef, string blockRef, string journeyPatternRef) => $"""
<OperationalVehicleLogOffRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
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

    static string BuildPredefinedMessageXml(string messageCode, string description) => $"""
<PredefinedMessageRequest xmlns:xs="http://www.w3.org/2001/XMLSchema" xs:version="1.0" xs:dateTime="{UtcNow()}">
    <MessageId>{Guid.NewGuid()}</MessageId>
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

    static string BuildLiveAnnouncementXml(string announcementId, string contentUrl) => $"""
<ReceivedAnnouncement xmlns:xs="http://www.w3.org/2001/XMLSchema" xs:version="1.0" xs:dateTime="{UtcNow()}">
    <MessageId>{Guid.NewGuid()}</MessageId>
    <Announcement id="{announcementId}" content="{contentUrl}"/>
</ReceivedAnnouncement>
""";

    static string BuildNotificationResponseXml(string notificationId, string description) => $"""
<NotificationResponse xmlns:xs="http://www.w3.org/2001/XMLSchema" xs:version="1.0" xs:dateTime="{UtcNow()}">
    <MessageId>{Guid.NewGuid()}</MessageId>
    <Notification id="{notificationId}">
        <Description>{description}</Description>
        <SentTime>{UtcNow()}</SentTime>
    </Notification>
</NotificationResponse>
""";

    static string BuildDistressCallRequestXml() => $"""
<DistressCallRequest xmlns:xs="http://www.w3.org/2001/XMLSchema" xs:version="1.0" xs:dateTime="{UtcNow()}">
    <MessageId>{Guid.NewGuid()}</MessageId>
</DistressCallRequest>
""";

    static string UtcNow() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}