using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ServiceModel;
using ServiceAssembly;
using System.ServiceModel.Description;
using System.Xml;

namespace WPFHost
{
    public partial class Window1 : Window
    {
        public Window1()
        {
            InitializeComponent();
        }

        ServiceHost host;

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            buttonStart.IsEnabled = false;

            //настройка базовых адрессов

            Uri tcpAdrs = new Uri("net.tcp://" + textBoxIP.Text.ToString() + ":" + 
                textBoxPort.Text.ToString() + "/WPFHost/");

            Uri httpAdrs = new Uri("http://" + textBoxIP.Text.ToString() + ":" +
                (int.Parse(textBoxPort.Text.ToString()) + 1).ToString() + "/WPFHost/");

            Uri[] baseAdresses = { tcpAdrs, httpAdrs };

            host = new ServiceHost(typeof(ServiceAssembly.ChatService), baseAdresses);


            NetTcpBinding tcpBinding = new NetTcpBinding(SecurityMode.None, true);
            //передача файлов размером до 64МБ
            tcpBinding.MaxBufferPoolSize = (int)67108864;
            tcpBinding.MaxBufferSize = 67108864;
            tcpBinding.MaxReceivedMessageSize = (int)67108864;
            tcpBinding.TransferMode = TransferMode.Buffered;
            tcpBinding.ReaderQuotas.MaxArrayLength = 67108864;
            tcpBinding.ReaderQuotas.MaxBytesPerRead = 67108864;
            tcpBinding.ReaderQuotas.MaxStringContentLength = 67108864;
            

            tcpBinding.MaxConnections = 100;

            //настройка троттлинга
            ServiceThrottlingBehavior throttle;
            throttle = host.Description.Behaviors.Find<ServiceThrottlingBehavior>();
            if (throttle == null)
            {
                throttle = new ServiceThrottlingBehavior();
                throttle.MaxConcurrentCalls = 100;
                throttle.MaxConcurrentSessions = 100;
                host.Description.Behaviors.Add(throttle);
            }


            //поддержка надежного сеанса в течение 20ч
            tcpBinding.ReceiveTimeout = new TimeSpan(20, 0, 0);
            tcpBinding.ReliableSession.Enabled = true;
            tcpBinding.ReliableSession.InactivityTimeout = new TimeSpan(20, 0, 10);

            host.AddServiceEndpoint(typeof(ServiceAssembly.IChat), tcpBinding, "tcp");

            //создание метаданных конечной точки, где будет публиковаться информация о сервисе
            ServiceMetadataBehavior mBehave = new ServiceMetadataBehavior();
            host.Description.Behaviors.Add(mBehave);

            host.AddServiceEndpoint(typeof(IMetadataExchange),
                MetadataExchangeBindings.CreateMexTcpBinding(),
                "net.tcp://" + textBoxIP.Text.ToString() + ":" + 
                (int.Parse(textBoxPort.Text.ToString()) - 1).ToString() + "/WPFHost/mex");


            try
            {
                host.Open();
            }
            catch (Exception ex)
            {
                labelStatus.Content = ex.Message.ToString();
            }
            finally
            {
                if (host.State == CommunicationState.Opened)
                {
                    labelStatus.Content = "Opened";
                    buttonStop.IsEnabled = true;
                }
            }
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            if (host != null)
            {
                try
                {
                    host.Close();
                }
                catch (Exception ex)
                {
                    labelStatus.Content = ex.Message.ToString();
                }
                finally
                {
                    if (host.State == CommunicationState.Closed)
                    {
                        labelStatus.Content = "Closed";
                        buttonStart.IsEnabled = true;
                        buttonStop.IsEnabled = false;
                    }
                }
            }
        }
    }
}
