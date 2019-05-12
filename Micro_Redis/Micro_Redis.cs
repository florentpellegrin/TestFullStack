using System;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using StackExchange.Redis;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using System.IO;

public class Startup
{
    public Startup(IHostingEnvironment env)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();
        Configuration = builder.Build();
    }

    public IConfigurationRoot Configuration { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Add framework services.
        // Add application services.
        services.AddTransient<IRedis>(s => new Redis("127.0.0.1:6379", "channel", true));
        var provider = services.BuildServiceProvider();
    }

    public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
    {
        // Set up application pipeline
    }
}

class Micro_Redis
{
    public static void Main(string[] args)
    {

            var host = new WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .UseIISIntegration()
        .UseStartup<Startup>()
        .UseApplicationInsights()
        .Build();

            host.Start();

        //Get the service added
        var myService = host.Services.GetService<IRedis>();


        Console.WriteLine("Création de la connexion au Redis");
        //Create Redis connection
        myService.CreateRedis();

        
        while (true)
        {
            Console.WriteLine("Press 1 to insert data :");
            Console.WriteLine("Press 2 to Quit :");

            string userChoice = "";
            userChoice = Console.ReadLine();

            if (userChoice == "1")
            {
                Console.WriteLine("Enter data to insert . Format to respect :[NameData1] [Data1] [NameData2] [Data2] .....");
                string dataToInsert = Console.ReadLine();


                //Parsing datas entered
                string[] datas = dataToInsert.Split(" ");
                
                if(datas.Length % 2 != 0)
                {
                    Console.WriteLine("You did not respect the format !");
                }
                else
                {
                    int nbInsert = datas.Length / 2, count = 0;
                    for(int i = 0; i < nbInsert; i++)
                    {
                        //Insert data
                        myService.InsertDataAsync(new string[2] { datas[count], datas[count + 1] }, myService.GetNbDataAdded());
                        count = count + 2;
                    }

                    myService.IncrNbDataAdded();


                }
            }

            if (userChoice == "2")
            {
                myService.Destroy();
                return;
            }
        }

    }
}

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

public class Redis : IRedis
{
	/// <summary>
	/// Declaration of variables
	/// </summary>
	private SocketManager mgr;
	private ConnectionMultiplexer connection;
	private IDatabase db;
	private int nbDataAdded = 0;
	private ITransaction transaction;
	private string connectionString;
	private string channelName;
	private RedisChannel channelRedis;
	private bool eventSubPub;
	private Dictionary<string,int> messageDict = new Dictionary<string, int>();


	/// <summary>
	/// Constructor of Redis Class
	/// </summary>
	/// <param name="connString"></param>
	public Redis(string connString = "127.0.0.1:6379" , string channelRedisName = "channel", bool subPubEvent = false)
	{
		this.connectionString = connString;
		this.channelName = channelRedisName;
		this.eventSubPub = subPubEvent;

		if(eventSubPub)
			channelRedis = new RedisChannel(this.channelName, RedisChannel.PatternMode.Auto);


	}

    public void PrintTest()
    {
        Console.WriteLine("Ceci est un test qui marche ");
    }
    /// <summary>
    /// Implementation of IRedis method GetConstructorParameter to get Parameters of the constructor
    /// </summary>
    /// <returns></returns>
    public string GetConnectionParameter()
	{
		return connectionString;
	}

	/// <summary>
	/// Implementation of IRedis method GetConstructorParameter to get Parameters of the constructor
	/// </summary>
	/// <returns></returns>
	public string GetChannelNameParameter()
	{
		return channelName;
	}

	/// <summary>
	/// Cration and connection to Redis database
	/// </summary>
	public void CreateRedis()
	{
		//Set Redis Option
		var options = ConfigurationOptions.Parse(GetConnectionParameter());
		//Connection to Redis
		connection = ConnectionMultiplexer.Connect(options);
		//Connection to database
		db = connection.GetDatabase();

		//Create Transaction
		transaction = db.CreateTransaction();
		
		if(eventSubPub)
		{
			//Subscribe to message from channel
			ISubscriber sub = connection.GetSubscriber();
			sub.Subscribe(this.channelName, (channel, message) => {
				Console.WriteLine("Subscribe message :" + (string)message);
			});
		}

	}

	/// <summary>
	/// Implementation of Destroy IRedis interface
	/// </summary>
	public void Destroy()
	{
		//Delete data in db
		for(int i=0;i<nbDataAdded;i++)
		{
			db.KeyDelete("data:" + i.ToString());
		}

		mgr?.Dispose();
		connection?.Dispose();
		mgr = null;
		db = null;
		connection = null;
	}

	/// <summary>
	/// Insert data to Redis
	/// </summary>
	/// <param name="username">
	/// String username is the name of the data to insert
	/// </param>
	/// <param name="datas">
	/// Datas is a string array containing data of the Redis data to insert
	/// </param>
	public void InsertDataAsync(string[] datas , int nbData )
	{
		if (datas.Length != 2)
		{
			Console.WriteLine("datas[].Length must be 2");
			return;

		}

		//Add datas in a Hashes map
		Console.WriteLine("Insert data '{0}' : '{1}' in Hashes data:'{2}", datas[0], datas[1], nbData);
		transaction.HashSetAsync("data:" + nbData.ToString(), new HashEntry[] { new HashEntry(datas[0], datas[1])});

		//Execution of the transaction
		transaction.Execute();


	}

	/// <summary>
	/// Increment NbDataAdded
	/// </summary>
	public void IncrNbDataAdded()
	{
		nbDataAdded++;

		if (eventSubPub)
		{
			if (nbDataAdded % 10 == 0)
			{
				//Pub/Sub event every 10 datas inserted
				db.Publish(channelRedis, nbDataAdded + " datas inserted");

			}


		}
	}

	/// <summary>
	/// Return the number of data added
	/// </summary>
	/// <returns></returns>
	public int GetNbDataAdded()
	{
		return nbDataAdded;
	}

	/// <summary>
	/// Add the data into the dictionnary with the corresponding data index
	/// </summary>
	/// <param name="message"></param>
	public void AddMessageToDict(string message)
	{
		messageDict.Add(message, nbDataAdded);
	}

	/// <summary>
	/// Get the corresponding index of data corresponding to the message send
	/// </summary>
	/// <param name="message"></param>
	/// <returns></returns>
	public int GetCorrespondingDataIndex(string message)
	{
		messageDict.TryGetValue(message, out int index);
		return index;
	}
}