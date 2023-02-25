using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WPFClient
{
    /// <summary>
    /// Interaction logic for RegWindow.xaml
    /// </summary>
    public partial class RegWindow : Window
    {
        DispatcherTimer dispatcherTimer = new DispatcherTimer();
        AdvancedChatEntities db = new AdvancedChatEntities();
        public RegWindow()
        {
            InitializeComponent();
            
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if(RegTxtBoxName.Text!="" && RegTxtBoxPassword.Password != "" && 
                RegTxtBoxRepeatPassword.Password==RegTxtBoxPassword.Password)
            {
                RegButton.IsEnabled = true;
            }
            else
            {
                RegButton.IsEnabled = false;
            }
        }

        private void RegButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ChatUser usr = db.ChatUser.FirstOrDefault(i => i.Name == RegTxtBoxName.Text);
                if (usr == null)
                {
                    if (RegTxtBoxPassword.Password.Length < 8)
                    {
                        MessageBox.Show("Пароль слишком короткий!");
                    }
                    else
                    {
                        db.ChatUser.Add(new ChatUser()
                        {
                            Name = RegTxtBoxName.Text,
                            Password = RegTxtBoxPassword.Password
                        });
                        db.SaveChanges();
                        MessageBox.Show("Вы успешно зарегистрировались!");
                        dispatcherTimer.Stop();
                        this.DialogResult = true;
                    }
                }
                else
                {
                    MessageBox.Show("Данное имя уже занято!");
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void RegTxtBoxRepeatPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if(RegTxtBoxPassword.Password == RegTxtBoxRepeatPassword.Password)
            {
                RegTxtBoxRepeatPassword.Foreground = Brushes.Green;
            }
            else
            {
                RegTxtBoxRepeatPassword.Foreground = Brushes.Red;
            }
            if(e.Key==Key.Enter && RegTxtBoxPassword.Password == RegTxtBoxRepeatPassword.Password)
            {
                if (RegButton.IsEnabled)
                {
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(RegButton);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                    RegButton.IsEnabled = false; 
                }
            }
        }

        private void RegTxtBoxName_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                RegTxtBoxPassword.Focus();
            }
        }

        private void RegTxtBoxPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RegTxtBoxRepeatPassword.Focus();
            }
        }
    }
}
