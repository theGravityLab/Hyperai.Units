namespace Hyperai.Units
{
    public struct Channel
    {
        public long UserId { get; set; }
        public long? GroupId { get; set; }

        public Channel Create(long userId, long? groupId = null)
        {
            return new Channel() { UserId = userId, GroupId = groupId };
        }

        public bool Match(long userId, long? groupId = null)
        {
            return UserId == userId && (GroupId == null || GroupId == groupId);
        }
    }
}
