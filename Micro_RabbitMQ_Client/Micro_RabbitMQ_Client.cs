using System;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;


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
}
	
class Micro_RabbitMQ_Client
{
    public static void Main()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var myService = provider.GetService<IRedis>();

        //Create Connection to Factory
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            //RabbitMQ Exchange declaration
            channel.ExchangeDeclare("logs", "fanout");

            var queueName = channel.QueueDeclare().QueueName;

            channel.QueueBind(queue: queueName,
                  exchange: "logs",
                  routingKey: "");

            Console.WriteLine("[*] Waiting for logs.");

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine(" [x] {0}", message);

                //Register to Redis database
                RedisRegister(message, myService);


            };
            channel.BasicConsume(queue: queueName,
                                 autoAck: true,
                                 consumer: consumer);

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }     

    }

    /// <summary>
    /// Register message to send in a Redis data
    /// </summary>
    /// <param name="message"></param>
    /// <param name="myService"></param>
    static void RedisRegister(string message, IRedis myService)
    {
        int indexData = myService.GetCorrespondingDataIndex(message);
        //Mise à jour du status
        myService.InsertDataAsync(new string[2] {"status", "received"  }, indexData);
        myService.InsertDataAsync(new string[2] {"date", GetCurrentDateTime() }, indexData);


    }

    /// <summary>
    /// Return the current date and time
    /// </summary>
    /// <returns></returns>
    static string GetCurrentDateTime()
    {
        string year = DateTime.Now.Year.ToString();
        string month = DateTime.Now.Month.ToString();
        string day = DateTime.Now.Day.ToString();
        string hour = DateTime.Now.Hour.ToString();
        string minute = DateTime.Now.Minute.ToString();
        string second = DateTime.Now.Second.ToString();
        return year + "_" + month + "_" + day + "_" + hour + "_" + minute + "_" + second;

    }
}