// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.DependencyInjection;
// using StateleSSE.AspNetCore.EfRealtime;
//
// namespace StateleSSE.AspNetCore;
//
// /// <summary>
// /// Extension methods for configuring EF Core realtime subscriptions.
// /// </summary>
// public static class EfRealtimeServiceCollectionExtensions
// {
//     /// <summary>
//     /// Adds EF Core realtime services (<see cref="IRealtimeManager"/> and the SaveChanges interceptor).
//     /// Call <see cref="AddEfRealtimeInterceptor"/> on your <c>DbContextOptionsBuilder</c> to wire it up.
//     /// </summary>
//     public static IServiceCollection AddEfRealtime(this IServiceCollection services)
//     {
//         services.AddSingleton<IRealtimeManager, RealtimeManager>();
//         services.AddSingleton<RealtimeSaveChangesInterceptor>();
//         return services;
//     }
//
//     /// <summary>
//     /// Adds the realtime SaveChanges interceptor to the DbContext options.
//     /// Must be called after <see cref="AddEfRealtime"/>.
//     /// <code>
//     /// builder.Services.AddEfRealtime();
//     /// builder.Services.AddDbContext&lt;MyDbContext&gt;((sp, options) =>
//     /// {
//     ///     options.UseNpgsql(connectionString);
//     ///     options.AddEfRealtimeInterceptor(sp);
//     /// });
//     /// </code>
//     /// </summary>
//     public static DbContextOptionsBuilder AddEfRealtimeInterceptor(
//         this DbContextOptionsBuilder builder,
//         IServiceProvider serviceProvider)
//     {
//         var interceptor = serviceProvider.GetRequiredService<RealtimeSaveChangesInterceptor>();
//         return builder.AddInterceptors(interceptor);
//     }
// }
