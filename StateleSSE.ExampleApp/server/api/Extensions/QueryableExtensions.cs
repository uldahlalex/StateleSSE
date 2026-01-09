using dataaccess;
using EF.Utilities;

namespace api.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<Quiz> IncludeAll(this IQueryable<Quiz> query)
    {
        return query.WithNavigations(maxDepth: 3, useSplitQuery: true);
    }

    public static IQueryable<Game> IncludeAll(this IQueryable<Game> query)
    {
        return query.WithNavigations(maxDepth: 3, useSplitQuery: true);
    }

    public static IQueryable<Gameround> IncludeAll(this IQueryable<Gameround> query)
    {
        return query.WithNavigations(maxDepth: 3, useSplitQuery: true);
    }

    public static IQueryable<Question> IncludeAll(this IQueryable<Question> query)
    {
        return query.WithNavigations(maxDepth: 2, useSplitQuery: false);
    }

    public static IQueryable<User> IncludeAll(this IQueryable<User> query)
    {
        return query.WithNavigations(maxDepth: 1, useSplitQuery: false);
    }

    public static IQueryable<Gamemember> IncludeAll(this IQueryable<Gamemember> query)
    {
        return query.WithNavigations(maxDepth: 2, useSplitQuery: false);
    }

    public static IQueryable<Answer> IncludeAll(this IQueryable<Answer> query)
    {
        return query.WithNavigations(maxDepth: 2, useSplitQuery: false);
    }

    public static IQueryable<Option> IncludeAll(this IQueryable<Option> query)
    {
        return query.WithNavigations(maxDepth: 1, useSplitQuery: false);
    }


}