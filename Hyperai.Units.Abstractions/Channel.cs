namespace Hyperai.Units
{
    public struct Channel
    {
        public long? UserId { get; set; }
        public long? GroupId { get; set; }

        public static Channel Create(long? userId, long? groupId = null)
        {
            return new Channel() { UserId = userId, GroupId = groupId };
        }

        public bool Match(long? userId, long? groupId = null)
        {
            return (UserId == null || UserId == userId) && (GroupId == null || GroupId == groupId);
        }

        public static Channel CreateMatchingGroup(long group)
        {
            return new Channel() { UserId = null, GroupId = group };
        }

        public static Channel CreateMatchingUser(long user)
        {
            return new Channel() { UserId = user, GroupId = null };
        }
    }
}