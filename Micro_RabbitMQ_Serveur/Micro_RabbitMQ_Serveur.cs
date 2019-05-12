using System;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System.Net;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;



public interface IRedis
{
	string GetConnectionParameter();
	string GetChannelNameParameter();
	void CreateRedis();
	void InsertDataAsync(string[] datas, int nbData);
	void Destroy();
	void IncrNbDataAdded();
	int GetNbDataAdded();
	void AddMessageToDict(string message);
	int GetCorrespondingDataIndex(string message);
    void PrintTest();

}
	
class Micro_RabbitMQ_Serveur
{
    public static void Main()
    {

        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var myService = provider.GetService<IRedis>();
        var myService = (IRedis)provider.GetService(typeof(IRedis));


        //Create Connection to Factory
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using (var connection = factory.CreateConnection())
        {
            using (var channel = connection.CreateModel())
            {
                //RabbitMQ Exchange declaration
                channel.ExchangeDeclare("logs", "fanout");

                while (true)
                {
                    Console.WriteLine("Press 1 to send message :");
                    Console.WriteLine("Press 2 to Quit :");

                    string userChoice = Console.ReadLine();

                    if (userChoice == "1")
                    {
                        Console.WriteLine("Enter message to send :");
                        string messageToSend = Console.ReadLine();

                        //Register to Redis database
                        RedisRegister(messageToSend, myService);


                        //Send to RabbitMQ exchange
                        SendMessageToExchange(messageToSend, channel);


                    }

                    if (userChoice == "2")
                    {
                        myService.Destroy();
                        return;
                    }
                }

            }

        }

    }


    /// <summary>
    /// Register message to send in a Redis data
    /// </summary>
    /// <param name="message"></param>
    /// <param name="myService"></param>
    static void RedisRegister(string message, IRedis myService)
    {
        myService.InsertDataAsync(new string[2] { "dataNumber", myService.GetNbDataAdded().ToString()}, myService.GetNbDataAdded());
        myService.InsertDataAsync(new string[2] { "message", message}, myService.GetNbDataAdded());
        myService.AddMessageToDict(message);
        myService.InsertDataAsync(new string[2] {"status", "send"  }, myService.GetNbDataAdded());
        myService.IncrNbDataAdded();

    }

    /// <summary>
    /// Send message to Exchange
    /// </summary>
    /// <param name="message"></param>
    /// <param name="channel"></param>
    static void SendMessageToExchange( string message, IModel channel)
    {
        var body = Encoding.UTF8.GetBytes(message);

        channel.BasicPublish(exchange: "logs",
                             routingKey: "",
                             basicProperties: null,
                             body: body);

        Console.WriteLine(" [x] Sent {0}", message);
    }
}