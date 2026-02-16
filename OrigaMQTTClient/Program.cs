using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("localhost", 1883)
            .WithClientId($"test-client-{Guid.NewGuid()}")
            .Build();

        client.ApplicationMessageReceivedAsync += e =>
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            Console.WriteLine("✅ RESPONSE RECEIVED:");
            Console.WriteLine($"📩 Topic: {e.ApplicationMessage.Topic}");
            Console.WriteLine(payload);
            Console.WriteLine();

            return Task.CompletedTask;
        };

        Console.WriteLine("Connecting to broker...");
        await client.ConnectAsync(options, CancellationToken.None);
        Console.WriteLine("✅ Connected");

        // --- SUBSCRIPTIONS ---
        await client.SubscribeAsync("vdv/test/login/response");
        await client.SubscribeAsync("vdv/test/logout/response");
        await client.SubscribeAsync("vdv/test/operational_login/response");
        await client.SubscribeAsync("vdv/test/operational_logout/response");

        // Subscribe to Predefined Message Response
        await client.SubscribeAsync("vdv/test/predefined_message/response");

        // Subscribe to Technical Vehicle LogOn/LogOff Responses
        await client.SubscribeAsync("vdv/test/technical_login/response");
        await client.SubscribeAsync("vdv/test/technical_logout/response");

        Console.WriteLine("✅ Subscribed to all response topics");

        // --- TEST FLOW ---
        Console.WriteLine("\n🚀 STARTING TESTS...\n");

        // Technical LogOn/LogOff (NEW)
        await SendTechnicalLogOnRequest(client);
        await Task.Delay(1000);

        await SendTechnicalLogOffRequest(client);
        await Task.Delay(1000);

        // Driver LogOn/LogOff
        await SendLogOnRequest(client);
        await Task.Delay(1000);

        await SendLogOffRequest(client);
        await Task.Delay(1000);

        // Operational LogOn/LogOff
        await SendOperationalLogOnRequest(client);
        await Task.Delay(1000);

        await SendOperationalLogOffRequest(client);
        await Task.Delay(1000);

        // Predefined Message
        await SendPredefinedMessageRequest(client);
        await Task.Delay(1000);

        // Gnss (Fire and Forget)
        await SendGnssPhysicalPositionRequest(client);
        await Task.Delay(1000);

        // Live Announcement
        await SendLiveAnnouncementRequest(client);
        await Task.Delay(1000);

        // Notification
        await SendNotificationResponse(client);
        await Task.Delay(1000);

        // Distress Call
        await SendDistressCallRequest(client);


        Console.WriteLine("\n👂 Listening for responses (Press Ctrl+C to quit)...\n");
        await Task.Delay(-1);
    }

    // ---------------- NEW: TECHNICAL VEHICLE LOGON / LOGOFF ----------------

    static async Task SendTechnicalLogOnRequest(IMqttClient client)
    {
        var topic = "vdv/test/technical_login";
        var payload = BuildTechnicalLogOnXml("de:mvg:5812", "obu-123", "2025-08-14.1");
        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ Technical LogOn Request Sent");
    }

    static async Task SendTechnicalLogOffRequest(IMqttClient client)
    {
        var topic = "vdv/test/technical_logout";
        var payload = BuildTechnicalLogOffXml("de:mvg:5812");
        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ Technical LogOff Request Sent");
    }

    static string BuildTechnicalLogOnXml(string vehicleRef, string obuId, string baseVersion) => $"""
<TechnicalVehicleLogOnRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" nameOfRefClass="Vehicle" version="1.0" />
    <OnboardUnitId>{obuId}</OnboardUnitId>
    <BaseVersion>{baseVersion}</BaseVersion>
    <Extensions>
        <VendorExtension>dummy-value</VendorExtension>
    </Extensions>
</TechnicalVehicleLogOnRequestStructure>
""";

    static string BuildTechnicalLogOffXml(string vehicleRef) => $"""
<TechnicalVehicleLogOffRequestStructure xmlns:netex="http://www.netex.org.uk/netex">
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0" />
    <Extensions>
        <VendorExtension>dummy-value</VendorExtension>
    </Extensions>
</TechnicalVehicleLogOffRequestStructure>
""";

    // ---------------- NEW: LIVE ANNOUNCEMENT ----------------

    static async Task SendLiveAnnouncementRequest(IMqttClient client)
    {
        var topic = "vdv/test/announcement";
        var payload = BuildLiveAnnouncementXml("123", "https://url_to_audio_file");

        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ Live Announcement Request Sent");
    }

    static string BuildLiveAnnouncementXml(string announcementId, string contentUrl)
    {
        return $"""
<ReceivedAnnouncement 
  xmlns:xs="http://www.w3.org/2001/XMLSchema"
  xs:version="1.0"
  xs:dateTime="{UtcNow()}">
  <MessageId>{Guid.NewGuid()}</MessageId>
  <Announcement id="{announcementId}" content="{contentUrl}"/>
</ReceivedAnnouncement>
""";
    }

    // ---------------- NEW: NOTIFICATION ----------------

    static async Task SendNotificationResponse(IMqttClient client)
    {
        var topic = "vdv/test/notification";
        var payload = BuildNotificationResponseXml("123", "Experiencing delay due to traffic");

        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ Notification Response Sent");
    }

    static string BuildNotificationResponseXml(string notificationId, string description)
    {
        return $"""
<NotificationResponse
  xmlns:xs="http://www.w3.org/2001/XMLSchema"
  xs:version="1.0"
  xs:dateTime="{UtcNow()}">
  <MessageId>{Guid.NewGuid()}</MessageId>
  <Notification id="{notificationId}">
	<Description>{description}</Description>
	<SentTime>{UtcNow()}</SentTime>
  </Notification>
</NotificationResponse>
""";
    }

    // ---------------- NEW: DISTRESS CALL ----------------

    static async Task SendDistressCallRequest(IMqttClient client)
    {
        var topic = "vdv/test/distress";
        var payload = BuildDistressCallRequestXml();

        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ Distress Call Request Sent");
    }

    static string BuildDistressCallRequestXml()
    {
        return $"""
<DistressCallRequest
  xmlns:xs="http://www.w3.org/2001/XMLSchema"
  xs:version="1.0"
  xs:dateTime="{UtcNow()}">
  <MessageId>{Guid.NewGuid()}</MessageId>
</DistressCallRequest>
""";
    }


    // ---------------- PREDEFINED MESSAGE ----------------

    static async Task SendPredefinedMessageRequest(IMqttClient client)
    {
        var topic = "vdv/test/predefined_message";
        // Example: Driver sends code "10" (Traffic Jam)
        var payload = BuildPredefinedMessageXml("10", "Traffic Jam - 10 min delay");

        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ Predefined Message Request Sent");
    }

    static string BuildPredefinedMessageXml(string messageCode, string description)
    {
        return $"""
<PredefinedMessageRequest 
    xmlns:xs="http://www.w3.org/2001/XMLSchema"
    xs:version="1.0" 
    xs:dateTime="{UtcNow()}">
    <MessageId>{Guid.NewGuid()}</MessageId>
    <MessageData description="{description}"/>
</PredefinedMessageRequest>
""";
    }

    // ---------------- LOGON / LOGOFF ----------------

    static async Task SendLogOnRequest(IMqttClient client)
    {
        var topic = "vdv/test/login";
        var payload = BuildLogOnXml("de:mvg:5812", "de:mvg:abc");
        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ LogOn Request Sent");
    }

    static async Task SendLogOffRequest(IMqttClient client)
    {
        var topic = "vdv/test/logout";
        var payload = BuildLogOffXml("de:mvg:5812", "de:mvg:abc");
        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ LogOff Request Sent");
    }

    // ---------------- OPERATIONAL LOGON / LOGOFF ----------------

    static async Task SendOperationalLogOnRequest(IMqttClient client)
    {
        var topic = "vdv/test/operational_login";
        var payload = BuildOperationalLogOnXml(
            vehicleRef: "de:mvg:1234",
            vehicleJourneyRef: "vehicleJourney:12345",
            operatingDayRef: "operatingDay:67890",
            blockRef: "block:54321",
            journeyPatternRef: "de:mvg:12345"
        );

        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ Operational LogOn Request Sent");
    }

    static async Task SendOperationalLogOffRequest(IMqttClient client)
    {
        var topic = "vdv/test/operational_logout";
        var payload = BuildOperationalLogOffXml(
            vehicleRef: "de:mvg:1234",
            vehicleJourneyRef: "vehicleJourney:12345",
            operatingDayRef: "operatingDay:67890",
            blockRef: "block:54321",
            journeyPatternRef: "de:mvg:12345"
        );

        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ Operational LogOff Request Sent");
    }

    // ---------------- ACTIVE RIDE (GNSS) ----------------

    static async Task SendGnssPhysicalPositionRequest(IMqttClient client)
    {
        var topic = "IoM/1.0/DataVersion/1.0/Country/DE/BE/Organisation/MVG/100/Vehicle/BUS/1234/PhysicalPosition/GnssPhysicalPositionData";
        var payload = BuildGnssPhysicalPositionXml();
        await PublishAsync(client, topic, payload);
        Console.WriteLine("✅ GnssPhysicalPosition Request Sent");
    }

    // ---------------- SHARED PUBLISHER ----------------

    static async Task PublishAsync(IMqttClient client, string topic, string payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await client.PublishAsync(message, CancellationToken.None);
    }

    // ---------------- XML BUILDERS ----------------

    static string BuildLogOnXml(string vehicleRef, string driverRef) => $"""
<DriverVehicleLogOnRequestStructure>
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <netex:DriverRef ref="{driverRef}" version="1.0"/>
</DriverVehicleLogOnRequestStructure>
""";

    static string BuildLogOffXml(string vehicleRef, string driverRef) => $"""
<DriverVehicleLogOffRequestStructure>
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <netex:DriverRef ref="{driverRef}" version="1.0"/>
    <Extensions>
        <VendorExtension>dummy-value</VendorExtension>
    </Extensions>
</DriverVehicleLogOffRequestStructure>
""";

    static string BuildOperationalLogOnXml(string vehicleRef, string vehicleJourneyRef, string operatingDayRef, string blockRef, string journeyPatternRef) => $"""
<OperationalVehicleLogOnRequestStructure>
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <DatedJourneyRef>
        <VehicleJourneyRef ref="{vehicleJourneyRef}" nameOfRefClass="VehicleJourney" modification="new" versionRef="1.0" created="{UtcNow()}" changed="{UtcNow()}" version="1.0"/>
        <OperatingDayRef ref="{operatingDayRef}" nameOfRefClass="OperatingDay" modification="revise" versionRef="1.0" created="{UtcNow()}" changed="{UtcNow()}" version="1.0"/>
        <BlockRef ref="{blockRef}" nameOfRefClass="Block" modification="new" versionRef="1.0" created="{UtcNow()}" changed="{UtcNow()}" version="1.0"/>
    </DatedJourneyRef>
    <netex:JourneyPatternRef ref="{journeyPatternRef}" nameOfRefClass="JourneyPattern" modification="new" versionRef="1.0" created="{UtcNow()}" changed="{UtcNow()}" version="1.0"/>
    <Extensions/>
</OperationalVehicleLogOnRequestStructure>
""";

    static string BuildOperationalLogOffXml(string vehicleRef, string vehicleJourneyRef, string operatingDayRef, string blockRef, string journeyPatternRef) => $"""
<OperationalVehicleLogOffRequestStructure>
    <Timestamp>{UtcNow()}</Timestamp>
    <Version>1.0</Version>
    <MessageId>{Guid.NewGuid()}</MessageId>
    <netex:VehicleRef ref="{vehicleRef}" version="1.0"/>
    <DatedJourneyRef>
        <VehicleJourneyRef ref="{vehicleJourneyRef}" nameOfRefClass="VehicleJourney" modification="new" versionRef="1.0" created="{UtcNow()}" changed="{UtcNow()}" version="1.0"/>
        <OperatingDayRef ref="{operatingDayRef}" nameOfRefClass="OperatingDay" modification="revise" versionRef="1.0" created="{UtcNow()}" changed="{UtcNow()}" version="1.0"/>
        <BlockRef ref="{blockRef}" nameOfRefClass="Block" modification="new" versionRef="1.0" created="{UtcNow()}" changed="{UtcNow()}" version="1.0"/>
    </DatedJourneyRef>
    <netex:JourneyPatternRef ref="{journeyPatternRef}" nameOfRefClass="JourneyPattern" modification="new" versionRef="1.0" created="{UtcNow()}" changed="{UtcNow()}" version="1.0"/>
    <Extensions/>
</OperationalVehicleLogOffRequestStructure>
""";

    static string BuildGnssPhysicalPositionXml() => $"""
<GnssPhysicalPositionDataStructure>
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

    static string UtcNow() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}