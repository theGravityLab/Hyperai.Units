using Hyperai.Events;
using Hyperai.Middlewares;
using Hyperai.Relations;
using Hyperai.Services;

namespace Hyperai.Units
{
    public class UnitMiddleware : IMiddleware
    {
        private readonly IUnitService _service;

        public UnitMiddleware(IUnitService service)
        {
            _service = service;
        }
        public bool Run(IApiClient sender, GenericEventArgs args)
        {
            MessageContext context = new MessageContext()
            {
                SentAt = args.Time,
                Client = sender,
                Me = sender.RequestAsync<Self>(null).GetAwaiter().GetResult(),
                Group = null
            };
            switch (args)
            {
                case GroupMessageEventArgs gm:
                    context.Group = gm.Group;
                    context.User = gm.User;
                    context.Message = gm.Message;
                    context.Type = MessageEventType.Group;
                    break;
                case FriendMessageEventArgs fm:
                    context.User = fm.User;
                    context.Message = fm.Message;
                    context.Type = MessageEventType.Friend;
                    break;
            }
            _service.Handle(context);
            return true;
        }
    }
}
