using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace VKFriendsSorter
{
    public partial class MainForm : Form
    {
        public string AccessToken { get; private set; }
        public string UserId { get; private set; }
        public VkUser[] Friends { get; private set; }
        public VkPost[] Posts { get; private set; }

        private ConcurrentExecutor cexec = new ConcurrentExecutor();
        private List<Thread> _miscThreads = new List<Thread>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            var authForm = new AuthForm();
            if (authForm.ShowDialog() != DialogResult.OK)
                Close();
            AccessToken = authForm.AccessToken;
            UserId = authForm.UserId;
            button2.Enabled = true;

            label1.Text = "Token: " + AccessToken;
            label2.Text = "User ID: " + UserId;

            label3.Text = "?";
            label4.Text = "?";
        }

        private void SetProgress(ProgressBar pb, Label l, int value)
        {
            var f = new Action<int>((i) => { pb.Value = i; });
            if (pb.InvokeRequired)
                pb.Invoke(f, value);
            else f(value);

            var f2 = new Action<int>((i) => { l.Text = pb.Value + "/" + pb.Maximum; });
            if (l.InvokeRequired)
                l.Invoke(f2, value);
            else f2(value);
        }
        private void SetMaxProgress(ProgressBar pb, int value)
        {
            var f = new Action<int>((i) => { pb.Maximum = i; });
            if (pb.InvokeRequired)
                pb.Invoke(f, value);
            else f(value);
        }

        private void SyncAddProgress(ProgressBar pb, Label l, int value)
        {
            lock (pb)
            {
                SetProgress(pb, l, pb.Value + value);
            }
        }

        private IEnumerable<VkUser> GetFriends()
        {
            var doc = SendApiRequestXml(true, "friends.get", "fields=nickname", "order=hints").DocumentElement;
            if (doc == null) yield break;
            var friends = doc.ChildNodes.Cast<XmlElement>();
            foreach (var fn in friends)
            {
                var user = new VkUser();
                foreach (var inf in fn.ChildNodes.Cast<XmlElement>())
                {
                    switch (inf.Name)
                    {
                        case "uid":
                            user.Uid = int.Parse(inf.InnerText);
                            break;
                        case "first_name":
                            user.FirstName = inf.InnerText;
                            break;
                        case "last_name":
                            user.SecondName = inf.InnerText;
                            break;
                    }
                }
                yield return user;
            } 
        }
        private IEnumerable<VkPost> GetWallPosts()
        {
            var offset = 0;
            var doc = SendApiRequestXml(true, "wall.get", "offset=" + offset, "count=" + 100, "filter=owner").DocumentElement;
            if (doc == null) yield break;
            var posts = doc.ChildNodes.Cast<XmlElement>();
            var totalCount = 0;
            foreach (var post in posts)
            {
                if (post.Name == "count")
                {
                    totalCount = int.Parse(post.InnerText);
                    SetMaxProgress(progressBar1, totalCount);
                    continue;
                }
                var vkpost = new VkPost();
                foreach (var field in post.ChildNodes.Cast<XmlElement>())
                {
                    XmlElement subfield;
                    switch (field.Name)
                    {
                        case "id":
                            vkpost.ID = int.Parse(field.InnerText);
                            break;
                        case "comments":
                            subfield = field.ChildNodes.Cast<XmlElement>().FirstOrDefault(e => e.Name == "count");
                            if (subfield != null)
                                vkpost.CommentsCount = int.Parse(subfield.InnerText);
                            break;
                        case "likes":
                            subfield = field.ChildNodes.Cast<XmlElement>().FirstOrDefault(e => e.Name == "count");
                            if (subfield != null)
                                vkpost.LikesCount = int.Parse(subfield.InnerText);
                            break;
                    }
                }
                offset++;
                yield return vkpost;
            }
            for (; offset < totalCount;)
            {
                foreach (var vkpost in GetWallPosts(offset, 100))
                {
                    offset++;
                    yield return vkpost;
                }
                SetProgress(progressBar1, label6, offset);
            }
        }
        private IEnumerable<VkPost> GetWallPosts(int offset, int count = 100)
        {
            var doc = SendApiRequestXml(true, "wall.get", "offset=" + offset, "count=" + count, "filter=owner").DocumentElement;
            if (doc == null) yield break;
            var posts = doc.ChildNodes.Cast<XmlElement>();
            foreach (var post in posts)
            {
                if (post.Name != "post")
                    continue;
                var vkpost = new VkPost();
                foreach (var field in post.ChildNodes.Cast<XmlElement>())
                {
                    XmlElement subfield;
                    switch (field.Name)
                    {
                        case "id":
                            vkpost.ID = int.Parse(field.InnerText);
                            break;
                        case "comments":
                            subfield = field.ChildNodes.Cast<XmlElement>().FirstOrDefault(e => e.Name == "count");
                            if (subfield != null)
                                vkpost.CommentsCount = int.Parse(subfield.InnerText);
                            break;
                        case "likes":
                            subfield = field.ChildNodes.Cast<XmlElement>().FirstOrDefault(e => e.Name == "count");
                            if (subfield != null)
                                vkpost.LikesCount = int.Parse(subfield.InnerText);
                            break;
                    }
                }
                yield return vkpost;
            }
        }

        const int MAX_LIKES_PER_PACKET = 1000;
        const int MAX_COMMENTS_PER_PACKET = 100;
        private void CalculateFriendsLikesForPostAsync(VkPost post)
        {
            lock (cexec)
                cexec.PushTaskNoInterrupt(() => CalculateFriendsLikesForPost(post));
        }
        private void CalculateFriendsLikesForPost(VkPost post)
        {
            var offset = 0;
            var doc = SendApiRequestXml(true, "likes.getList", 
                "offset=0",
                "count=" + MAX_LIKES_PER_PACKET,
                "item_id=" + post.ID, 
                "type=post").DocumentElement;
            if (doc == null) return;
            var likes = doc.ChildNodes.Cast<XmlElement>();
            var totalCount = 0;
            foreach (var o in likes)
            {
                if (o.Name == "count")
                {
                    totalCount = int.Parse(o.InnerText);
                    continue;
                }
                if (o.Name == "uid")
                {
                    offset++;
                    var id = int.Parse(o.InnerText);
                    post.Likers.Add(id);
                }
            }
            for (; offset < totalCount; )
                CalculateFriendsLikesForPost(post, ref offset);
            
        }
        private void CalculateFriendsLikesForPost(VkPost post, ref int offset, int count = MAX_LIKES_PER_PACKET)
        {
            var doc = SendApiRequestXml(true, "likes.getList", 
                "offset=" + offset, 
                "count=" + count, 
                "item_id=" + post.ID, 
                "type=post").DocumentElement;
            if (doc == null) return;
            var likesList = doc.ChildNodes.Cast<XmlElement>().FirstOrDefault(e => e.Name == "users");
            if (likesList == null) return;
            var likes = likesList.ChildNodes.Cast<XmlElement>();
            foreach (var liker in likes.Where(like => like.Name == "uid"))
            {
                offset++;
                var id = int.Parse(liker.InnerText);
                post.Likers.Add(id);
            }
        }

        private void CalculateFriendsCommentsForPostAsync(VkPost post)
        {
            lock (cexec)
                cexec.PushTaskNoInterrupt(() => CalculateFriendsCommentsForPost(post));
        }
        private void CalculateFriendsCommentsForPost(VkPost post)
        {
            var offset = 0;
            var doc = SendApiRequestXml(false, "wall.getComments",
                "owner_id" + UserId,
                "offset=0",
                "count=" + MAX_COMMENTS_PER_PACKET,
                "preview_length=1",
                "post_id=" + post.ID).DocumentElement;
            if (doc == null) return;
            var likes = doc.ChildNodes.Cast<XmlElement>();
            var totalCount = 0;
            foreach (var o in likes)
            {
                if (o.Name == "count")
                {
                    totalCount = int.Parse(o.InnerText);
                    continue;
                }
                if (o.Name != "comment")
                    continue;
                offset++;
                var authorInfo = o.ChildNodes.Cast<XmlElement>();
                var authorIdInfo = authorInfo.FirstOrDefault(field => field.Name == "from_id");
                if (authorIdInfo == null) continue;

                var id = int.Parse(authorIdInfo.InnerText);
                post.Commenters.Add(id);
            }
            for (; offset < totalCount; )
                CalculateFriendsCommentsForPost(post, ref offset);
            
        }
        private void CalculateFriendsCommentsForPost(VkPost post, ref int offset, int count = MAX_COMMENTS_PER_PACKET)
        {
            var doc = SendApiRequestXml(false, "wall.getComments",
                "owner_id" + UserId,
                "offset=" + offset,
                "count=" + count,
                "preview_length=1",
                "post_id=" + post.ID).DocumentElement;
            if (doc == null) return;
            var likes = doc.ChildNodes.Cast<XmlElement>();
            foreach (var author in likes.Where(like => like.Name == "comment"))
            {
                offset++;
                var authorInfo = author.ChildNodes.Cast<XmlElement>();
                var authorIdInfo = authorInfo.FirstOrDefault(field => field.Name == "from_id");
                if (authorIdInfo == null) continue;
                
                var id = int.Parse(authorIdInfo.InnerText);
                post.Commenters.Add(id);
            }
        }

        private string SendApiRequest(bool sendToken, string method, params string[] args)
        {
            var uri = "https://api.vk.com/method/" + method + "?" + string.Join("&", args);
            if (sendToken)
                uri+= "&access_token=" + AccessToken;
            var r = WebRequest.CreateHttp(uri);
            Stream str;
            try
            {
                str = r.GetResponse().GetResponseStream();
            }
            catch (Exception e)
            {
                return "";
            }
            if (str == null)
                return "";
            using (var s = new StreamReader(str))
            {
                try { return s.ReadToEnd(); }
                catch (Exception e) { return ""; }
            }
        }
        private XmlDocument SendApiRequestXml(bool sendToken, string method, params string[] args)
        {
            var s = SendApiRequest(sendToken, method + ".xml", args);
            var xml = new XmlDocument();
            xml.LoadXml(s);
            return xml;
        }
        private void workerGetPostList_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            workerGetPostList.ReportProgress(0);
            Posts = GetWallPosts().ToArray();
            workerGetPostList.ReportProgress(progressBar1.Maximum);

            Invoke(new Action(() =>
            {
                progressBar1.Enabled = false;
                label3.Text = "Готово";
                label6.Text = "";
            }));
        }
        private void workerGetPostList_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            var thr = new Thread(() =>
            {
                cexec.AddWorkers(512);
                foreach (var vkpost in Posts)
                {
                    if (vkpost.LikesCount > 0)
                        CalculateFriendsLikesForPostAsync(vkpost);
                    if (vkpost.CommentsCount > 0)
                        CalculateFriendsCommentsForPostAsync(vkpost);
                }
                Invoke(new Action(() =>
                {
                    progressBar2.Enabled = true;
                    label4.Text = "Получаю id лайкнувших и авторов комментариев ...";
                    progressBar2.Maximum = cexec.TasksLeftToDo;
                    cexec.SignalAll();
                    waitForFinishTimer.Enabled = true;
                }));
            });
            _miscThreads.Add(thr);
            thr.Start();
        }

        private void waitForFinishTimer_Tick(object sender, EventArgs e)
        {
            lock (cexec)
            {
                SetProgress(progressBar2, label7, progressBar2.Maximum - cexec.TasksLeftToDo);
                if (!cexec.IsComplete)
                    return;

                waitForFinishTimer.Enabled = false;
                label4.Text = "Готово";
                label7.Text = "";
                Analyze();


                button1.Enabled = button2.Enabled = true;
            }
        }

        public void Analyze()
        {
            foreach (var p in Posts)
            {
                foreach (var liker in p.Likers)
                {
                    var friend = Friends.FirstOrDefault(user => user.Uid == liker);
                    if (friend == null)
                        continue;
                    friend.Likes++;
                }
                foreach (var a in p.Commenters)
                {
                    var friend = Friends.FirstOrDefault(user => user.Uid == a);
                    if (friend == null)
                        continue;
                    friend.Comments++;
                }
            }
            var l = Friends.ToList();
            l.Sort(((a, b) =>
            {
                var va = a.Likes + a.Comments;
                var vb = b.Likes + b.Comments;
                return va < vb ? 1 : (va == vb ? 0 : -1);
            }));
            Friends = l.ToArray();
            FillList();
        }

        private void FillList()
        {
            listBox1.Items.Clear();
            foreach (var friend in Friends)
                listBox1.Items.Add(friend);
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var lid = listBox1.SelectedIndex;
            if (lid < 0)
                return;

            var selectedUser = (VkUser) listBox1.Items[lid];
            Process.Start("http://vk.com/id" + selectedUser.Uid);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            button1.Enabled = false;
            var thr = new Thread(() =>
            {
                button1.Invoke(new Action(() => { Friends = GetFriends().ToArray(); }));
                Invoke(new Action(() =>
                {
                    FillList();
                    progressBar1.Enabled = true;
                    label3.Text = "Получаю список постов ...";
                    workerGetPostList.RunWorkerAsync();
                }));
            });
            _miscThreads.Add(thr);
            thr.Start();
        }

        private void label2_DoubleClick(object sender, EventArgs e)
        {
            Process.Start("http://vk.com/id" + UserId);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            cexec.KillAll();
            foreach (var t in _miscThreads)
            {
                t.Interrupt();
                try { t.Join(100); }
                catch (ThreadInterruptedException) {}
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var authForm = new AuthForm { LogOut = true, AccessToken = AccessToken };
            authForm.ShowDialog();
            AccessToken = "";
            UserId = "";
            listBox1.Items.Clear();
            authForm = new AuthForm();
            if (authForm.ShowDialog() != DialogResult.OK)
                Close();
        }
    }
}
