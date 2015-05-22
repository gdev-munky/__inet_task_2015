using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VKFriendsSorter
{
    public class VkPost
    {
        public int ID { get; set; }
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }

        public List<int> Likers = new List<int>();
        public List<int> Commenters = new List<int>();
    }
}
