using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
    /// Interaction logic for BlackListWindow.xaml
    /// </summary>
    public partial class BlackListWindow : Window
    {
        AdvancedChatEntities db = new AdvancedChatEntities();
        DispatcherTimer dispatcherTimer = new DispatcherTimer();

        //для определения того, какую ф-цию должна делать кнопка toggle
        //0 - добавление в черный список 1 - удаление из черного списка
        private int _buttonMode = -1;
        public BlackListWindow()
        {
            InitializeComponent();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();


            foreach (var item in db.ChatUser.Where(i=>db.BlackList.FirstOrDefault(j=>j.ChatUser.Id == i.Id) == null && i.Name!="admin"))
            {
                ListBoxItem itm = MakeItem(item.Name, item.Id);
                WhiteList.Items.Add(itm);
            }

            foreach (var item in db.BlackList.Include("ChatUser").ToList())
            {
                ListBoxItem itm = MakeItem(item.ChatUser.Name, item.ChatUser.Id);
                BlackList.Items.Add(itm);
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if(WhiteList.SelectedItem != null || BlackList.SelectedItem != null)
            {
                buttonToggle.IsEnabled = true;
            }
            else
            {
                buttonToggle.IsEnabled = false;
            }
        }

        private ListBoxItem MakeItem( string name, int usrId)
        {
            ListBoxItem item = new ListBoxItem();

            TextBlock txtblock = new TextBlock();
            txtblock.Text = name + " ID: "+usrId.ToString();
            txtblock.VerticalAlignment = VerticalAlignment.Center;

            StackPanel panel = new StackPanel();
            panel.Orientation = Orientation.Horizontal;
            panel.Children.Add(item);
            panel.Children.Add(txtblock);

            ListBoxItem bigItem = new ListBoxItem();
            bigItem.Content = panel;

            return bigItem;
        }

        private void buttonToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_buttonMode == 0)
            {
                //получение id и name выбранного юзера
                string txt = ((((ListBoxItem)WhiteList.SelectedItem).Content as StackPanel).Children[1] as TextBlock).Text;
                int id = Convert.ToInt32(txt.Remove(0, txt.IndexOf(":")+2));
                string name = txt.Remove(txt.IndexOf(" ID:"));

                WhiteList.Items.Remove((ListBoxItem)WhiteList.SelectedItem);
                ListBoxItem newItem = MakeItem(name, id);
                BlackList.Items.Add(newItem);

                try
                {
                    var usr = db.ChatUser.First(i => i.Id == id);
                    db.BlackList.Add(new BlackList { ChatUser = usr });
                }
                catch (SqlException ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else
            {
                string txt = ((((ListBoxItem)BlackList.SelectedItem).Content as StackPanel).Children[1] as TextBlock).Text;
                int id = Convert.ToInt32(txt.Remove(0, txt.IndexOf(":") + 2));
                string name = txt.Remove(txt.IndexOf(" ID:"));

                BlackList.Items.Remove((ListBoxItem)BlackList.SelectedItem);
                ListBoxItem newItem = MakeItem(name, id);
                WhiteList.Items.Add(newItem);

                try
                {
                    var usr = db.BlackList.First(i => i.ChatUser.Id == id);
                    db.BlackList.Remove(usr);
                }
                catch (SqlException ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void WhiteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BlackList.SelectedItem = null;
            _buttonMode = 0;
        }

        private void BlackList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WhiteList.SelectedItem = null;
            _buttonMode = 1;
        }

        private void ButtonSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                db.SaveChanges();
                MessageBox.Show("Изменения успешно сохранены!");
            }
            catch (SqlException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ButtonSaveChanges_MouseEnter(object sender, MouseEventArgs e)
        {
            buttonToggle.Foreground = Brushes.Black;
        }

        private void buttonToggle_MouseEnter(object sender, MouseEventArgs e)
        {
            if (buttonToggle.IsEnabled)
            {
                imgArrows.Source = new BitmapImage(new Uri(@"\img\leftrightarrow.png", UriKind.Relative));
                buttonToggle.Background = Brushes.White;
                buttonToggle.BorderBrush = Brushes.Black; 
            }
        }

        private void buttonToggle_MouseLeave(object sender, MouseEventArgs e)
        {
            if (buttonToggle.IsEnabled)
            {
                imgArrows.Source = new BitmapImage(new Uri(@"\img\leftrightarrowwhite.png", UriKind.Relative));
                buttonToggle.Background = Brushes.Transparent;
                buttonToggle.BorderBrush = Brushes.White; 
            }
        }

        private void buttonToggle_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (buttonToggle.IsEnabled)
            {
                imgArrows.Source = new BitmapImage(new Uri(@"\img\leftrightarrowwhite.png", UriKind.Relative));
                imgArrows.Opacity = 1;
            }
            else
            {
                buttonToggle.Background = Brushes.Transparent;
                imgArrows.Opacity = 0.5;
            }
        }
    }
}
