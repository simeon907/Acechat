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
using System.IO;
using System.Reflection;
using System.ServiceModel;
using WPFClient.SVC;
using System.Collections;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using System.Net;
using System.Data.SqlClient;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Threading.Tasks;

namespace WPFClient
{
    public partial class Window1 : Window, SVC.IChatCallback
    {
        SVC.ChatClient proxy = null;
        SVC.Client receiver = null;
        SVC.Client localClient = null;

        //база данных
        AdvancedChatEntities db = new AdvancedChatEntities();

        DispatcherTimer dispatcherTimer = new DispatcherTimer();
        bool isBlocked = false;//эта переменная будет помогать определять, заблокирован ли пользователь

        //сохранненные файлы
        string rcvFilesPath = @"C:/WCF_Received_Files/";

        //для обработки состояний прокси в отдельном потоке
        private delegate void FaultedInvoker();

        //список контактов
        Dictionary<ListBoxItem, SVC.Client> OnlineClients = new Dictionary<ListBoxItem, Client>();


        public Window1()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(Window1_Loaded);
            chatListBoxNames.SelectionChanged += new SelectionChangedEventHandler(chatListBoxNames_SelectionChanged);
            chatTxtBoxType.KeyDown += new KeyEventHandler(chatTxtBoxType_KeyDown);
            chatTxtBoxType.KeyUp += new KeyEventHandler(chatTxtBoxType_KeyUp);
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
        }



        //методы для вызова обработки состояния прокси
        //если состояние faulted или closed, нужно установить proxy = null, для его повторного запуска в дальнейшем
        //эти методы выводятся в отдельном потоке
        void InnerDuplexChannel_Closed(object sender, EventArgs e)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FaultedInvoker(HandleProxy));
                return;
            }
            HandleProxy();
        }

        void InnerDuplexChannel_Opened(object sender, EventArgs e)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FaultedInvoker(HandleProxy));
                return;
            }
            HandleProxy();
        }

        void InnerDuplexChannel_Faulted(object sender, EventArgs e)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FaultedInvoker(HandleProxy));
                return;
            }
            HandleProxy();
        }

        #region Private Methods

        //метод для обрабоки состояния прокси
        private void HandleProxy()
        {
            if (proxy != null)
            {
                switch (this.proxy.State)
                {
                    case CommunicationState.Closed:
                        proxy = null;
                        chatListBoxMsgs.Items.Clear();
                        chatListBoxNames.Items.Clear();
                        loginLabelStatus.Content = "Отключен";
                        ShowChat(false);
                        ShowLogin(true);
                        loginButtonConnect.IsEnabled = true;
                        dispatcherTimer.Stop();
                        break;
                    case CommunicationState.Closing:
                        break;
                    case CommunicationState.Created:
                        break;
                    case CommunicationState.Faulted:
                        proxy.Abort();
                        proxy = null;
                        chatListBoxMsgs.Items.Clear();
                        chatListBoxNames.Items.Clear();
                        ShowChat(false);
                        ShowLogin(true);
                        loginLabelStatus.Content = "Отключен";
                        loginButtonConnect.IsEnabled = true;
                        dispatcherTimer.Stop();
                        break;
                    case CommunicationState.Opened:
                        ShowLogin(false);
                        ShowChat(true);

                        chatLabelCurrentStatus.Content = "Онлайн";
                        chatLabelCurrentUName.Content = this.localClient.Name;

                        Dictionary<int, Image> images = GetImages();
                        Image img = images[loginComboBoxImgs.SelectedIndex];
                        chatCurrentImage.Source = img.Source;
                        dispatcherTimer.Start();
                        break;
                    case CommunicationState.Opening:
                        break;
                    default:
                        break;
                }
            }

        }

        private void Connect()
        {
            if (proxy == null)
            {
                try
                {
                    this.localClient = new SVC.Client();
                    this.localClient.Name = loginTxtBoxUName.Text.ToString();
                    this.localClient.AvatarID = loginComboBoxImgs.SelectedIndex;
                    InstanceContext context = new InstanceContext(this);
                    proxy = new SVC.ChatClient(context);

                    
                    string servicePath = proxy.Endpoint.ListenUri.AbsolutePath;
                    string serviceListenPort = proxy.Endpoint.Address.Uri.Port.ToString();

                    proxy.Endpoint.Address = new EndpointAddress("net.tcp://" + loginTxtBoxIP.Text.ToString() + ":" + serviceListenPort + servicePath);


                    proxy.Open();

                    proxy.InnerDuplexChannel.Faulted += new EventHandler(InnerDuplexChannel_Faulted);
                    proxy.InnerDuplexChannel.Opened += new EventHandler(InnerDuplexChannel_Opened);
                    proxy.InnerDuplexChannel.Closed += new EventHandler(InnerDuplexChannel_Closed);
                    proxy.ConnectAsync(this.localClient);
                    proxy.ConnectCompleted += new EventHandler<ConnectCompletedEventArgs>(proxy_ConnectCompleted);
                    
                    ChatUser user = db.ChatUser.FirstOrDefault(i => i.Name == this.localClient.Name);
                    db.ChatMessage.Add(new ChatMessage
                    {
                        AvatarId = this.localClient.AvatarID,
                        ChatUser = user,
                        Content = "------------ " + this.localClient.Name + " присоединился ------------" + "[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]"
                    });
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message.ToString());
                    loginLabelStatus.Content = "Офлайн";
                    loginButtonConnect.IsEnabled = true;
                }
            }
            else
            {
                HandleProxy();
            }
        }

        private void Send()
        {
            if (proxy != null && chatTxtBoxType.Text != "")
            {
                if (proxy.State == CommunicationState.Faulted)
                {
                    HandleProxy();
                }
                else
                {
                    SVC.Message msg = new WPFClient.SVC.Message();
                    msg.Sender = this.localClient.Name;
                    msg.Content = chatTxtBoxType.Text.ToString();

                    //если режим лс включен и контакт из списка контактов выбран,
                    //то ссобщение будет отправлено
                    if ((bool)chatCheckBoxWhisper.IsChecked)
                    {
                        if (this.receiver != null)
                        {
                            proxy.WhisperAsync(msg, this.receiver);
                            chatTxtBoxType.Text = "";
                            chatTxtBoxType.Focus();
                        }
                    }
                    else
                    {
                        string txt = this.localClient.Name + " : " + chatTxtBoxType.Text + " [" +DateTime.Now.ToShortDateString()+ " " + DateTime.Now.ToShortTimeString() + "] ";
                        Task.Run(() =>
                        {
                            try
                            {
                                using (AdvancedChatEntities database = new AdvancedChatEntities())
                                {
                                    ChatUser usr = database.ChatUser.FirstOrDefault(i => i.Name == this.localClient.Name);
                                    database.ChatMessage.Add(new ChatMessage
                                    {
                                        AvatarId = this.localClient.AvatarID,
                                        ChatUser = usr,
                                        Content = txt
                                    });
                                    database.SaveChanges();
                                }
                            }
                            catch (SqlException ex)
                            {
                                MessageBox.Show(ex.Message);
                            }
                        });

                        proxy.SayAsync(msg);
                        chatTxtBoxType.Text = "";
                        chatTxtBoxType.Focus();
                    }
                    //сигнал сервису, что пользователь закочил печатать
                    proxy.IsWritingAsync(null);
                }
            }
        }

        
        //скролл чата при отправке нового сообщения
        private ScrollViewer FindVisualChild(DependencyObject obj)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is ScrollViewer)
                {
                    return (ScrollViewer)child;
                }
                else
                {
                    ScrollViewer childOfChild = FindVisualChild(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        //создание сообщения
        private ListBoxItem MakeItem(int imgID, string text, bool withTime)
        {
            ListBoxItem item = new ListBoxItem();
            Dictionary<int, Image> images = GetImages();
            Image img = images[imgID];
            img.Height = 70;
            img.Width = 60;
            item.Content = img;

            string msgtxt;
            if (withTime)
            {
                msgtxt = text + " [" +DateTime.Now.ToShortDateString()+" " + DateTime.Now.ToShortTimeString() + "]";
                
            }
            else
            {
                msgtxt = text;
            }
            TextBlock txtblock = new TextBlock();
            txtblock.Text = msgtxt;
            txtblock.VerticalAlignment = VerticalAlignment.Center;

            

            StackPanel panel = new StackPanel();
            panel.Orientation = Orientation.Horizontal;
            panel.Children.Add(item);
            panel.Children.Add(txtblock);

            ListBoxItem bigItem = new ListBoxItem();
            bigItem.Content = panel;

            return bigItem;
        }

        //выгрузка изображений
        private Dictionary<int, Image> GetImages()
        {
            List<Stream> picsStrm = new List<Stream>();

            Assembly asmb = Assembly.GetExecutingAssembly();
            string[] picNames = asmb.GetManifestResourceNames();

            foreach (string s in picNames)
            {
                if (s.EndsWith(".png"))
                {
                    Stream strm = asmb.GetManifestResourceStream(s);
                    if (strm != null)
                    {
                        picsStrm.Add(strm);
                    }
                }
            }


            Dictionary<int, Image> images = new Dictionary<int, Image>();

            int i = 0;

            foreach (Stream strm in picsStrm)
            {

                PngBitmapDecoder decoder = new PngBitmapDecoder(strm,
                    BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                BitmapSource bitmap = decoder.Frames[0] as BitmapSource;
                Image img = new Image();
                img.Source = bitmap;
                img.Stretch = Stretch.UniformToFill;

                images.Add(i, img);
                i++;

                strm.Close();
            }
            return images;
        }

        
        private void ShowLogin(bool show)
        {
            if (show)
            {
                loginButtonConnect.Visibility = Visibility.Visible;
                loginComboBoxImgs.Visibility = Visibility.Visible;
                loginLabelIP.Visibility = Visibility.Visible;
                loginLabelStatus.Visibility = Visibility.Visible;
                loginLabelTitle.Visibility = Visibility.Visible;
                loginLabelUName.Visibility = Visibility.Visible;
                loginPolyLine.Visibility = Visibility.Visible;
                loginTxtBoxIP.Visibility = Visibility.Visible;
                loginTxtBoxUName.Visibility = Visibility.Visible;
            }
            else
            {
                loginButtonConnect.Visibility = Visibility.Collapsed;
                loginComboBoxImgs.Visibility = Visibility.Collapsed;
                loginLabelIP.Visibility = Visibility.Collapsed;
                loginLabelStatus.Visibility = Visibility.Collapsed;
                loginLabelTitle.Visibility = Visibility.Collapsed;
                loginLabelUName.Visibility = Visibility.Collapsed;
                loginPolyLine.Visibility = Visibility.Collapsed;
                loginTxtBoxIP.Visibility = Visibility.Collapsed;
                loginTxtBoxUName.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowChat(bool show)
        {
            if (show)
            {
                chatButtonDisconnect.Visibility = Visibility.Visible;
                chatButtonSend.Visibility = Visibility.Visible;
                chatCheckBoxWhisper.Visibility = Visibility.Visible;
                chatCurrentImage.Visibility = Visibility.Visible;
                chatLabelCurrentStatus.Visibility = Visibility.Visible;
                chatLabelCurrentUName.Visibility = Visibility.Visible;
                chatListBoxMsgs.Visibility = Visibility.Visible;
                chatListBoxNames.Visibility = Visibility.Visible;
                chatTxtBoxType.Visibility = Visibility.Visible;
                chatLabelWritingMsg.Visibility = Visibility.Visible;
                chatLabelSendFileStatus.Visibility = Visibility.Visible;
                chatButtonOpenReceived.Visibility = Visibility.Visible;
                chatButtonSendFile.Visibility = Visibility.Visible;
                ChatToolbar.Visibility = this.localClient.Name.ToLower() == "admin" ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                chatButtonDisconnect.Visibility = Visibility.Collapsed;
                chatButtonSend.Visibility = Visibility.Collapsed;
                chatCheckBoxWhisper.Visibility = Visibility.Collapsed;
                chatCurrentImage.Visibility = Visibility.Collapsed;
                chatLabelCurrentStatus.Visibility = Visibility.Collapsed;
                chatLabelCurrentUName.Visibility = Visibility.Collapsed;
                chatListBoxMsgs.Visibility = Visibility.Collapsed;
                chatListBoxNames.Visibility = Visibility.Collapsed;
                chatTxtBoxType.Visibility = Visibility.Collapsed;
                chatLabelWritingMsg.Visibility = Visibility.Collapsed;
                chatLabelSendFileStatus.Visibility = Visibility.Collapsed;
                chatButtonOpenReceived.Visibility = Visibility.Collapsed;
                chatButtonSendFile.Visibility = Visibility.Collapsed;
                ChatToolbar.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region UI_Events

        void Window1_Loaded(object sender, RoutedEventArgs e)
        {
            DirectoryInfo dir = new DirectoryInfo(rcvFilesPath);
            dir.Create();

            Dictionary<int, Image> images = GetImages();
            
            foreach (Image img in images.Values)
            {
                ListBoxItem item = new ListBoxItem();
                item.Width = 90;
                item.Height = 90;
                item.Content = img;

                loginComboBoxImgs.Items.Add(item);
            }
            loginComboBoxImgs.SelectedIndex = 0;
            

            ShowChat(false);
            ShowLogin(true);

            loginTxtBoxUName.Focus();
        }

        private void chatButtonOpenReceived_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(rcvFilesPath);
        }

        private void chatButtonSendFile_Click(object sender, RoutedEventArgs e)
        {
            if (this.receiver != null)
            {
                Stream strm = null;
                try
                {
                    OpenFileDialog fileDialog = new OpenFileDialog();
                    fileDialog.Multiselect = false;

                    if (fileDialog.ShowDialog() == DialogResult.HasValue)
                    {
                        return;
                    }

                    strm = fileDialog.OpenFile();
                    if (strm != null)
                    {
                        byte[] buffer = new byte[(int)strm.Length];

                        int i = strm.Read(buffer, 0, buffer.Length);

                        if (i > 0)
                        {
                            SVC.FileMessage fMsg = new FileMessage();
                            fMsg.FileName = fileDialog.SafeFileName;
                            fMsg.Sender = this.localClient.Name;
                            fMsg.Data = buffer;
                            proxy.SendFileAsync(fMsg, this.receiver);
                            proxy.SendFileCompleted += new EventHandler<SendFileCompletedEventArgs>(proxy_SendFileCompleted);
                            chatLabelSendFileStatus.Content = "Отправка...";
                        }

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    if (strm != null)
                    {
                        strm.Close();
                    }
                }
            }

        }

        void proxy_SendFileCompleted(object sender, SendFileCompletedEventArgs e)
        {
            chatLabelSendFileStatus.Foreground = Brushes.White;
            chatLabelSendFileStatus.Content = "Файл отправлен";
        }

        //когда последний пользователь покидает чат,
        //то сообщение об его уходе не отправляется,
        //этот метод это исправляет
        private void LastUserLeave()
        {
            if (OnlineClients.Count==1)
            {
                ListBoxItem item = MakeItem(this.localClient.AvatarID,
                                        "------------ " + this.localClient.Name + " покинул чат ------------", true);
                chatListBoxMsgs.Items.Add(item);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (proxy != null)
            {
                if (proxy.State == CommunicationState.Opened)
                {
                    try
                    {
                        ChatUser usr = db.ChatUser.FirstOrDefault(i => i.Name == this.localClient.Name);
                        db.ChatMessage.Add(new ChatMessage
                        {
                            AvatarId = this.localClient.AvatarID,
                            ChatUser = usr,
                            Content = "------------ " + this.localClient.Name + " покинул чат ------------" + "[" +DateTime.Now.ToShortDateString()+" "+ DateTime.Now.ToShortTimeString() + "]"
                        });
                        db.SaveChanges();
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                    LastUserLeave();
                    //при proxy.close() будет вызываться handleproxy()
                    proxy.Disconnect(this.localClient);
                    dispatcherTimer.Stop();
                }
                else
                {
                    HandleProxy();
                }
            }
        }

        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ChatUser usr = db.ChatUser.FirstOrDefault(i => i.Name == loginTxtBoxUName.Text && i.Password == loginTxtBoxPassword.Password);
                if (usr != null)
                {
                    loginButtonConnect.IsEnabled = false;
                    loginLabelStatus.Content = "Подключение...";
                    
                    foreach (var i in db.ChatMessage)
                    {
                        ListBoxItem itm = MakeItem(i.AvatarId, i.Content, false);
                        chatListBoxMsgs.Items.Add(itm);
                    }

                    proxy = null;
                    Connect();
                    ScrollViewer sv = FindVisualChild(chatListBoxMsgs);
                    sv.ScrollToBottom();
                    
                    dispatcherTimer.Start();
                }
                else
                {
                    MessageBox.Show("Неверный логин или пароль!");
                    loginButtonConnect.IsEnabled = true;
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void proxy_ConnectCompleted(object sender, ConnectCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                loginLabelStatus.Foreground = new SolidColorBrush(Colors.Red);
                MessageBox.Show(e.Error.Message.ToString());
                loginButtonConnect.IsEnabled = true;
            }
            else if (e.Result)
            {
                HandleProxy();
            }
            else if (!e.Result)
            {
                loginLabelStatus.Content = "Имя найдено";
                loginButtonConnect.IsEnabled = true;
            }
        }

        private void chatButtonSend_Click(object sender, RoutedEventArgs e)
        {
            Send();
        }

        private void chatButtonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (proxy != null)
            {
                if (proxy.State == CommunicationState.Faulted)
                {
                    HandleProxy();
                }
                else
                {
                    try
                    {
                        ChatUser usr = db.ChatUser.FirstOrDefault(i => i.Name == this.localClient.Name);
                        db.ChatMessage.Add(new ChatMessage
                        {
                            AvatarId = this.localClient.AvatarID,
                            ChatUser = usr,
                            Content = "------------ " + this.localClient.Name + " покинул чат ------------" + "[" +DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]"
                        });
                        db.SaveChanges();
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                    LastUserLeave();
                    proxy.Disconnect(this.localClient);
                    dispatcherTimer.Stop();
                }
            }
        }


        void chatTxtBoxType_KeyUp(object sender, KeyEventArgs e)
        {
            if (proxy != null)
            {
                if (proxy.State == CommunicationState.Faulted)
                {
                    HandleProxy();
                }
                else
                {
                    if (chatTxtBoxType.Text.Length < 1)
                    {
                        proxy.IsWritingAsync(null);
                    }
                }
            }
        }

        //отправка сообщения если пользователь нажмет enter
        void chatTxtBoxType_KeyDown(object sender, KeyEventArgs e)
        {
            if (proxy != null)
            {
                if (proxy.State == CommunicationState.Faulted)
                {
                    HandleProxy();
                }
                else
                {
                    if (e.Key == Key.Enter)
                    {
                        Send();
                    }
                    else if (chatTxtBoxType.Text.Length < 1)
                    {
                        proxy.IsWritingAsync(this.localClient);
                    }
                }
            }
        }

        void chatListBoxNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBoxItem item = chatListBoxNames.SelectedItem as ListBoxItem;
            if (item != null)
            {
                this.receiver = this.OnlineClients[item];
            }
        }

        private void loginTxtBlockReg_Click(object sender, RoutedEventArgs e)
        {
            RegWindow rw = new RegWindow();
            rw.ShowDialog();
        }
            
        private void ButtonShowBlackList_Click(object sender, RoutedEventArgs e)
        {
            BlackListWindow blw = new BlackListWindow();
            blw.ShowDialog();
        }

        private void loginTxtBoxIP_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                loginTxtBoxUName.Focus();
            }
        }

        private void loginTxtBoxUName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                loginTxtBoxPassword.Focus();
            }
        }

        private void loginTxtBoxPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (loginButtonConnect.IsEnabled)
                {
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(loginButtonConnect);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                    loginButtonConnect.IsEnabled = false; 
                }
            }
        }

        //Проверка, есть ли пользователь в черном списке
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var localClientId = db.ChatUser.FirstOrDefault(i => i.Name == this.localClient.Name).Id;
                if (db.BlackList.FirstOrDefault(i => i.ChatUser.Id == localClientId) != null)
                {
                    chatTxtBoxType.Text = "Вам запретили отправлять сообщения в этот чат";
                    chatTxtBoxType.IsEnabled = false;
                    chatButtonSend.IsEnabled = false;
                    chatButtonSendFile.IsEnabled = false;
                    isBlocked = true;
                }
                else
                {
                    if (isBlocked)
                    {
                        chatTxtBoxType.Text = "";
                        isBlocked = false;
                    }
                    chatTxtBoxType.IsEnabled = true;
                    chatButtonSend.IsEnabled = true;
                    chatButtonSendFile.IsEnabled = true;
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        #endregion

        #region IChatCallback Members

        //обновление списка пользователей
        public void RefreshClients(List<WPFClient.SVC.Client> clients)
        {
            chatListBoxNames.Items.Clear();
            OnlineClients.Clear();
            foreach (SVC.Client c in clients)
            {
                ListBoxItem item = MakeItem(c.AvatarID, c.Name, false);
                chatListBoxNames.Items.Add(item);
                OnlineClients.Add(item, c);
            }
        }

        //получение сообщения
        public void Receive(WPFClient.SVC.Message msg)
        {
            foreach (SVC.Client c in this.OnlineClients.Values)
            {
                if (c.Name == msg.Sender)
                {
                    ListBoxItem item = MakeItem(c.AvatarID, msg.Sender + " : " + msg.Content, true);
                    chatListBoxMsgs.Items.Add(item);
                }
            }
            ScrollViewer sv = FindVisualChild(chatListBoxMsgs);
            sv.LineDown();
        }

        //получение личного сообщения
        public void ReceiveWhisper(WPFClient.SVC.Message msg, WPFClient.SVC.Client receiver)
        {
            foreach (SVC.Client c in this.OnlineClients.Values)
            {
                if (c.Name == msg.Sender)
                {
                    ListBoxItem item = MakeItem(c.AvatarID,
                        msg.Sender + " отправил(а) личное сообщение " + receiver.Name + " : " + msg.Content, true);
                    chatListBoxMsgs.Items.Add(item);
                }
            }
            ScrollViewer sv = FindVisualChild(chatListBoxMsgs);
            sv.LineDown();
        }

        //пользователь печатает...
        public void IsWritingCallback(WPFClient.SVC.Client client)
        {
            if (client == null)
            {
                chatLabelWritingMsg.Content = "";
            }
            else
            {
                chatLabelWritingMsg.Content += client.Name + " печатает..., ";
            }
        }

        //получение файла
        public void ReceiverFile(WPFClient.SVC.FileMessage fileMsg, WPFClient.SVC.Client receiver)
        {
            try
            {
                FileStream fileStrm = new FileStream(rcvFilesPath + fileMsg.FileName, FileMode.Create, FileAccess.ReadWrite);
                fileStrm.Write(fileMsg.Data, 0, fileMsg.Data.Length);
                chatLabelSendFileStatus.Foreground = Brushes.White;
                chatLabelSendFileStatus.Content = "Получен файл, " + fileMsg.FileName;
            }
            catch (Exception ex)
            {
                chatLabelSendFileStatus.Content = ex.Message.ToString();
            }
        }

        public void UserJoin(WPFClient.SVC.Client client)
        {
            ListBoxItem item = MakeItem(client.AvatarID,
                "------------ " + client.Name + " присоединился ------------", true);
            chatListBoxMsgs.Items.Add(item);
            ScrollViewer sv = FindVisualChild(chatListBoxMsgs);
            sv.LineDown();
        }

        public void UserLeave(WPFClient.SVC.Client client)
        {
            ListBoxItem item = MakeItem(client.AvatarID,
                "------------ " + client.Name + " покинул чат ------------", true);
            chatListBoxMsgs.Items.Add(item);
            ScrollViewer sv = FindVisualChild(chatListBoxMsgs);
            sv.LineDown();
        }

        

        #region Async

        public IAsyncResult BeginUserLeave(WPFClient.SVC.Client client, AsyncCallback callback, object asyncState)
        {
            throw new NotImplementedException();
        }

        public void EndUserLeave(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginUserJoin(WPFClient.SVC.Client client, AsyncCallback callback, object asyncState)
        {
            throw new NotImplementedException();
        }

        public void EndUserJoin(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginReceiverFile(WPFClient.SVC.FileMessage fileMsg, WPFClient.SVC.Client receiver, AsyncCallback callback, object asyncState)
        {
            throw new NotImplementedException();
        }

        public void EndReceiverFile(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginIsWritingCallback(WPFClient.SVC.Client client, AsyncCallback callback, object asyncState)
        {
            throw new NotImplementedException();
        }

        public void EndIsWritingCallback(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginReceiveWhisper(WPFClient.SVC.Message msg, WPFClient.SVC.Client receiver, AsyncCallback callback, object asyncState)
        {
            throw new NotImplementedException();
        }

        public void EndReceiveWhisper(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginReceive(WPFClient.SVC.Message msg, AsyncCallback callback, object asyncState)
        {
            throw new NotImplementedException();
        }

        public void EndReceive(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginRefreshClients(List<WPFClient.SVC.Client> clients, AsyncCallback callback, object asyncState)
        {
            throw new NotImplementedException();
        }

        public void EndRefreshClients(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        #endregion

        #endregion
    }
}
