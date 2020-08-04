using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Middlewares;
using Hyperai.Relations;
using Hyperai.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

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
            if (args is MessageEventArgs messageArgs)
            {
                // 该消息无法被序列化
                // 没必要继续下去
                if (messageArgs.Message.Any(x => x.GetType().GetCustomAttribute<SerializableAttribute>() == null))
                {
                    return true;
                }
            }
            else
            {
                // 连消息事件都不是, 就更没必要了
                return true;
            }
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
            _logger.LogDebug("Handling for Unit Actions took {} milliseconds(preparing = {}): {}", stopwatch.ElapsedMilliseconds + prepare, prepare, context.Message);
            stopwatch.Reset();
            return true;
        }
    }
}