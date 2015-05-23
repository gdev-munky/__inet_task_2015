using System;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace VKFriendsSorter
{
    public partial class AuthForm : Form
    {
        public bool LogOut { get; set; }
        public string AccessToken { get; set; }
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
            if (LogOut)
            {
                logout();
                DialogResult = DialogResult.OK;
                //Close();
                return;
            }
            login();
        }

        private void login()
        {
            webBrowser1.Navigate("https://oauth.vk.com/authorize?client_id=4926252" +
                                                               "&redirect_uri=https://oauth.vk.com/blank.html" +
                                                               "&scope=friends,wall" +
                                                               "&display=page" +
                                                               "&response_type=token");
        }
        private void logout()
        {
            webBrowser1.Navigate(
                "javascript:void((function(){var a,b,c,e,f;f=0;a=document.cookie.split('; ');for(e=0;e<a.length&&a[e];e++){f++;for(b='.'+location.host;b;b=b.replace(/^(?:%5C.|[^%5C.]+)/,'')){for(c=location.pathname;c;c=c.replace(/.$/,'')){document.cookie=(a[e]+'; domain='+b+'; path='+c+'; expires='+new Date((new Date()).getTime()-1e11).toGMTString());}}}})())");
        }
    }
}
