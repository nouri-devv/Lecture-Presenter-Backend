using Npgsql;
using FastMember;

public static class ExtensionMethods
{
    public static void MapTo<T>(this NpgsqlDataReader dr, T entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        var fastMember = TypeAccessor.Create(entity.GetType());
        var props = fastMember.GetMembers().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < dr.FieldCount; i++)
        {
            var columnName = dr.GetName(i);
            var prop = props.FirstOrDefault(x => x.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(prop))
            {
                var value = dr.IsDBNull(i) ? null : dr.GetValue(i);
                fastMember[entity, prop] = value;
            }
            else
            {
                Console.WriteLine($"Unmapped column: {columnName}");
            }
        }
    }
}
