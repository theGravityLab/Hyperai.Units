using Hyperai.Events;
using Hyperai.Middlewares;
using Hyperai.Relations;
using Hyperai.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Hyperai.Units
{
    public class UnitMiddleware : IMiddleware
    {
        private readonly IUnitService _service;
        private readonly ILogger _logger;

        private readonly Stopwatch stopwatch = new Stopwatch();

        public UnitMiddleware(IUnitService service, ILogger<UnitMiddleware> logger)
        {
            _service = service;
            _logger = logger;
        }

        public bool Run(IApiClient sender, GenericEventArgs args)
        {
            stopwatch.Start();
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

                default:
                    return true;
            }
            stopwatch.Stop();
            long prepare = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            _service.Handle(context);
            stopwatch.Stop();
            _logger.LogDebug("Handling for Unit Actions took {} milliseconds(routing = {}): {}", stopwatch.ElapsedMilliseconds + prepare, stopwatch.ElapsedMilliseconds, context.Message);
            stopwatch.Reset();
            return true;
        }
    }
}