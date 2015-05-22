using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace VKFriendsSorter
{
    public partial class AuthForm : Form
    {
        public string AccessToken { get; private set; }
        public string UserId { get; private set; }
        public bool Authenticated { get; private set; }
        public AuthForm()
        {
            InitializeComponent();
            DialogResult = DialogResult.No;
        }


        private void webBrowser1_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            if (!webBrowser1.Url.ToString().StartsWith("https://oauth.vk.com/blank.html"))
                return;
            AccessToken = Regex.Match(webBrowser1.Url.AbsoluteUri, "(?<=access_token=)[\\da-z]+").ToString();
            UserId = Regex.Match(webBrowser1.Url.AbsoluteUri, "(?<=user_id=)\\d+").ToString();

            Authenticated = AccessToken != "";
            DialogResult = Authenticated ? DialogResult.OK : DialogResult.No;
            Close();
        }

        private void AuthForm_Load(object sender, EventArgs e)
        {
            webBrowser1.Navigate("https://oauth.vk.com/authorize?client_id=4926252" +
                                                               "&redirect_uri=https://oauth.vk.com/blank.html" +
                                                               "&scope=messages,friends" +
                                                               "&display=page" +
                                                               "&response_type=token");
        }
    }
}
