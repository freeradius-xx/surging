using Autofac;
using Microsoft.Extensions.Logging;
using Surging.Core.Caching;
using Surging.Core.Caching.Configurations;
using Surging.Core.Codec.MessagePack;
using Surging.Core.Consul;
using Surging.Core.Consul.Configurations;
using Surging.Core.CPlatform;
using Surging.Core.CPlatform.Utilities;
using Surging.Core.DotNetty;
using Surging.Core.EventBusRabbitMQ;
using Surging.Core.EventBusRabbitMQ.Configurations;
using Surging.Core.Log4net;
using Surging.Core.Nlog;
using Surging.Core.ProxyGenerator;
using Surging.Core.ServiceHosting;
using Surging.Core.ServiceHosting.Internal.Implementation;
using Surging.Core.System.Intercept;
using Surging.IModuleServices.Common;
using System;
//using Surging.Core.Zookeeper;
//using Surging.Core.Zookeeper.Configurations;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Surging.Services.Client
{
    public class Program
    {
        private static int _endedConnenctionCount = 0;
        private static DateTime begintime;
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.WriteLine($"[{DateTime.Now.ToString()}]: Start to build Client......");
            var host = new ServiceHostBuilder()
                .RegisterServices(builder =>
                {
                    builder.AddMicroService(option =>
                    {
                        option.AddClient()
                        .AddClientIntercepted(typeof(CacheProviderInterceptor)) //20180706
                        //option.UseZooKeeperManager(new ConfigInfo("127.0.0.1:2181"));
                        .UseConsulManager(new ConfigInfo("127.0.0.1:8500")) //20180706
                        .UseDotNettyTransport()
                        //.UseRabbitMQTransport() //comment by zony on 20180704
                        .AddCache() //20180706
                        //.UseKafkaMQTransport(kafkaOption =>
                        //{
                        //    kafkaOption.Servers = "127.0.0.1";
                        //});
                        //.UseProtoBufferCodec()
                        .UseMessagePackCodec();
                        builder.Register(p => new CPlatformContainer(ServiceLocator.Current));
                        //builder.RegisterType(typeof(Modules.Common.Domain.UserService)).As<IUserService>(); //Surging.Modules.Common的引用于20180706添加，临时测试
                    });
                })
                .Configure(build =>
                build.AddEventBusFile("eventBusSettings.json", optional: false)) //20180706
                .Configure(build =>
                build.AddCacheFile("cacheSettings.json", optional: false, reloadOnChange: true)) //20180706
                .UseNLog(LogLevel.Trace)
                // .UseLog4net(LogLevel.Error)
                .UseServiceCache() //20180706
                .UseProxy() //20180706
                .UseClient() //20180706
                .UseStartup<Startup>()
                .Build();

            Console.WriteLine($"[{DateTime.Now.ToString()}]: Start to run Client......");
            using (host.Run())
            {
                Console.WriteLine($"[{DateTime.Now.ToString()}]: Run Client successfully.");
                Console.WriteLine($"Thread Id: {Environment.CurrentManagedThreadId}");

                //Startup.Test(ServiceLocator.GetService<IServiceProxyFactory>());
                //Startup.TestRabbitMq(ServiceLocator.GetService<IServiceProxyFactory>());
                //Startup.TestForRoutePath(ServiceLocator.GetService<IServiceProxyProvider>());
                //test Parallel
                //var connectionCount = 200000;
                //StartRequest(connectionCount);

                //Startup.TestLocalService(); //测试本地模块和服务调用

                Startup.TestRemoteService();

                Console.WriteLine($"[{DateTime.Now.ToString()}]: ReadLine to exit.");
                Console.ReadLine();
            }
        }

        private static void StartRequest(int connectionCount)
        {
            var service = ServiceLocator.GetService<IServiceProxyFactory>();
            var userProxy = service.CreateProxy<IUserService>("User");
            Parallel.For(0, connectionCount /1000, new ParallelOptions() { MaxDegreeOfParallelism = 10 },u =>
             {
                 for (var i = 0; i < 1000; i++)
                     Test(userProxy, connectionCount);
             });
        }

        public static void Test(IUserService userProxy,int connectionCount)
        {
            var a = userProxy.GetDictionary().Result;
            IncreaseSuccessConnection(connectionCount);
        }
        
        private static void IncreaseSuccessConnection(int connectionCount)
        {
            Interlocked.Increment(ref _endedConnenctionCount);
            if (_endedConnenctionCount == 1)
                begintime = DateTime.Now;
            if (_endedConnenctionCount >= connectionCount)
                Console.WriteLine($"结束时间{(DateTime.Now - begintime).TotalMilliseconds}");
        }
    }
}
