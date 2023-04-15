using System;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;
using System.Text;

namespace serviceSDK
{
    public class IotHubManager
    {
        private readonly ServiceClient client;
        private readonly RegistryManager registry;
        public IotHubManager(ServiceClient client, RegistryManager registry)
        {
            this.client = client;
            this.registry = registry;
        }

        //C2D
        public async Task SendMessage(string textMessage, string deviceId)
        {
            var messageBody = new { text = textMessage };
            var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
            message.MessageId = Guid.NewGuid().ToString();
            await client.SendAsync(deviceId, message);
        }

        public async Task<int> ExecuteDeviceMethod(string methodName, string deviceId)
        {
            var method = new CloudToDeviceMethod(methodName);

            var result = await client.InvokeDeviceMethodAsync(deviceId, method);
            return result.Status;
        }

        public async Task UpdateDesiredTwin(string deviceId, string propertyName, dynamic propertyValue)
        {
            var twin = await registry.GetTwinAsync(deviceId);
            twin.Properties.Desired[propertyName] = propertyName;
            await registry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
        }

    }

}

namespace serviceSDKConsole
{
    internal static class FeatureSelector
    {
        public static void PrintMenu()
        {
            Console.WriteLine(@"
            1-C2D
            2-Device Twin
            3-Direct Method-> Send Message
            4-Direct Method-> Emergency STOP
            5-Direct Method-> Reset Errors
            6- Increase Production Rate
            7- Decrease Production Rate
            0- Exit"
            );
        }

        public static async Task Execute(int feature, serviceSDK.IotHubManager manager)
        {
            switch (feature)
            {
                case 1:
                    {
                        System.Console.WriteLine("Wpisz i kliknij enter");
                        string messageText = System.Console.ReadLine() ?? string.Empty;

                        Console.WriteLine("daj ID dewajsa i kliknij enter");
                        string devideID = System.Console.ReadLine() ?? string.Empty;

                        await manager.SendMessage(messageText, devideID);
                    }
                    break;
                case 2:
                    {
                        Console.WriteLine("daj ID dewajsa i kliknij enter");
                        string devideID = System.Console.ReadLine() ?? string.Empty;

                        Console.WriteLine("Daj property Name:");
                        string propertyName = Console.ReadLine() ?? string.Empty;

                        var random = new Random();
                        await manager.UpdateDesiredTwin(devideID, propertyName, random.Next());
                    }
                    break;
                case 3:
                    {
                        Console.WriteLine("daj ID dewajsa i kliknij enter");
                        string devideID = System.Console.ReadLine() ?? string.Empty;

                        var result = await manager.ExecuteDeviceMethod("SendMessages", devideID);
                        Console.WriteLine("Method Executed with:");
                        Console.WriteLine(result);

                        /*
                        catch (DeviceNotFoundException)
                        {
                            Console.WriteLine("Device not connected");
                        }
                        */
                    }
                    break;
                case 4:
                    {
                        Console.WriteLine("daj ID dewajsa i kliknij enter");
                        string devideID = System.Console.ReadLine() ?? string.Empty;

                        var result = await manager.ExecuteDeviceMethod("EmergencyStop", devideID);
                        Console.WriteLine("Method Executed with:");
                        Console.WriteLine(result);

                        /*
                        catch (DeviceNotFoundException)
                        {
                            Console.WriteLine("Device not connected");
                        }
                        */
                    }
                    break;
                case 5:
                    {
                        Console.WriteLine("daj ID dewajsa i kliknij enter");
                        string devideID = System.Console.ReadLine() ?? string.Empty;

                        var result = await manager.ExecuteDeviceMethod("ClearErrors", devideID);
                        Console.WriteLine("Method Executed with:");
                        Console.WriteLine(result);

                        /*
                        catch (DeviceNotFoundException)
                        {
                            Console.WriteLine("Device not connected");
                        }
                        */
                    }
                    break;
                case 6:
                    {
                        Console.WriteLine("daj ID dewajsa i kliknij enter");
                        string devideID = System.Console.ReadLine() ?? string.Empty;

                        var result = await manager.ExecuteDeviceMethod("ChangeProdRateUP", devideID);
                        Console.WriteLine("Method Executed with:");
                        Console.WriteLine(result);

                        /*
                        catch (DeviceNotFoundException)
                        {
                            Console.WriteLine("Device not connected");
                        }
                        */
                    }
                    break;
                case 7:
                    {
                        Console.WriteLine("daj ID dewajsa i kliknij enter");
                        string devideID = System.Console.ReadLine() ?? string.Empty;

                        var result = await manager.ExecuteDeviceMethod("ChangeProdRateDOWN", devideID);
                        Console.WriteLine("Method Executed with:");
                        Console.WriteLine(result);

                        /*
                        catch (DeviceNotFoundException)
                        {
                            Console.WriteLine("Device not connected");
                        }
                        */
                    }
                    break;
                default:
                    break;
            }

        }

        internal static int ReadInput()
        {
            var keyPressed = Console.ReadKey();
            var isParsed = int.TryParse(keyPressed.KeyChar.ToString(), out int value);
            return isParsed ? value : -1;
        }
    }
}
