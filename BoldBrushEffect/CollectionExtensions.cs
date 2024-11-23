namespace BoldBrushEffect
{
    internal static class CollectionExtensions
    {
        public static IEnumerable<TValue> ValuesWhereKey<TKey, TValue>(this IDictionary<TKey, TValue> source, Func<TKey, bool> selector) => source.Where(x => selector(x.Key)).Select(x => x.Value);

        public static IEnumerable<TValue> ValuesWhereKeyDoesNotContain<TValue>(this IDictionary<string, TValue> source, params string[] wordsToExclude) => source.ValuesWhereKey(DoesNotContain(wordsToExclude));
        private static Func<string, bool> DoesNotContain(params string[] wordsToExclude) => (string value) => !wordsToExclude.Any(value.Contains);

        public static T[][] SwapAxes<T>(this T[][] array)
        {
            var newArray = Enumerable.Range(0, array[0].Length).Select(
                row => Enumerable.Range(0, array.Length).Select(column => array[column][row]).ToArray()
                ).ToArray();

            return newArray;
        }
    }
}
