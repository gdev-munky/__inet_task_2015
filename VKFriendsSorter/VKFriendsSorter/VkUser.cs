namespace VKFriendsSorter
{
    public class VkUser
    {
        public int Uid { get; set; }
        public string FirstName { get; set; }
        public string SecondName { get; set; }

        public int Likes { get; set; }
        public int Comments { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}]: {1} {2} ({3} likes, {4} comments)", Uid, FirstName, SecondName, Likes, Comments);
        }
    }
}
