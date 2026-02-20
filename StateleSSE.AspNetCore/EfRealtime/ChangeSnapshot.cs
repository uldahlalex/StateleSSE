// using Microsoft.EntityFrameworkCore;
//
// namespace StateleSSE.AspNetCore.EfRealtime;
//
// /// <summary>
// /// A point-in-time snapshot of entity changes captured before SaveChanges completes.
// /// Use the typed accessors to inspect what changed.
// /// </summary>
// public sealed class ChangeSnapshot
// {
//     private readonly IReadOnlyList<ChangeEntry> _entries;
//
//     internal ChangeSnapshot(IReadOnlyList<ChangeEntry> entries)
//     {
//         _entries = entries;
//     }
//
//     /// <summary>
//     /// All change entries in this snapshot.
//     /// </summary>
//     public IReadOnlyList<ChangeEntry> Entries => _entries;
//
//     /// <summary>
//     /// Get change entries for entities of a specific type.
//     /// </summary>
//     public IEnumerable<ChangeEntry<T>> OfType<T>() where T : class =>
//         _entries.Where(e => e.Entity is T).Select(e => new ChangeEntry<T>((T)e.Entity, e.State));
//
//     /// <summary>
//     /// Whether any entities of the given type were added, modified, or deleted.
//     /// </summary>
//     public bool HasChanges<T>() where T : class =>
//         _entries.Any(e => e.Entity is T);
//
//     /// <summary>
//     /// Whether any entities of the given type were added.
//     /// </summary>
//     public bool HasAdded<T>() where T : class =>
//         _entries.Any(e => e.Entity is T && e.State == EntityState.Added);
//
//     /// <summary>
//     /// Whether any entities of the given type were modified.
//     /// </summary>
//     public bool HasModified<T>() where T : class =>
//         _entries.Any(e => e.Entity is T && e.State == EntityState.Modified);
//
//     /// <summary>
//     /// Whether any entities of the given type were deleted.
//     /// </summary>
//     public bool HasDeleted<T>() where T : class =>
//         _entries.Any(e => e.Entity is T && e.State == EntityState.Deleted);
// }
//
// /// <summary>
// /// A single entity change entry with its pre-save state.
// /// </summary>
// public sealed record ChangeEntry(object Entity, EntityState State);
//
// /// <summary>
// /// A typed entity change entry.
// /// </summary>
// public sealed record ChangeEntry<T>(T Entity, EntityState State) where T : class;
