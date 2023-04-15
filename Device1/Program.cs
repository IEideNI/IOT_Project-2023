using System;
using System.Text;
using Microsoft.Azure.Devices.Client;
using Device;


string conString = File.ReadAllText(@"ConnectionString.txt");
Console.WriteLine("Connection String loaded");

using var deviceClient = DeviceClient.CreateFromConnectionString(conString);
await deviceClient.OpenAsync();
var device = new Device1(deviceClient);
Console.WriteLine("Connection with Device Established");
await device.InitializerHandlers();
await device.UpdateTwinAsync();
Console.WriteLine("Ready to WORK =)");
await device.SendMessages(2, 1000);
Console.ReadLine();
