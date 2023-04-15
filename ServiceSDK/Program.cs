using System;
using serviceSDK;
using serviceSDKConsole;
using Microsoft.Azure.Devices;

string conString = File.ReadAllText(@"ConnectionString.txt");
Console.WriteLine("Connection String loaded");


using var serviceClient = ServiceClient.CreateFromConnectionString(conString);
using var registryManager = RegistryManager.CreateFromConnectionString(conString);


var manager = new IotHubManager(serviceClient, registryManager);


int input;
do
{
    FeatureSelector.PrintMenu();
    input = FeatureSelector.ReadInput();
    await FeatureSelector.Execute(input, manager);
} while (input != 0);

Console.WriteLine("END");