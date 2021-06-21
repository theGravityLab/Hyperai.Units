﻿using Microsoft.Extensions.DependencyInjection;

namespace Hyperai.Units
{
    public static class Extensions
    {
        /// <summary>
        ///     add but no search
        /// </summary>
        /// <param name="services">service collection</param>
        /// <returns>original service collection</returns>
        public static IServiceCollection AddUnits(this IServiceCollection services)
        {
            services.AddSingleton<IUnitService, UnitService>();
            return services;
        }

        public static IHyperaiApplicationBuilder UseUnits(this IHyperaiApplicationBuilder builder)
        {
            builder.Use<UnitMiddleware>();
            return builder;
        }
    }
}
