using BrushStrokes;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
/*
{
Console.WriteLine("sqrt tests");
testSqrt1();
testSqrt2();
testSqrt3();
{
Console.WriteLine(nameof(testSqrt1));
Stopwatch s = Stopwatch.StartNew();
var result = testSqrt1();
s.Stop();
Console.WriteLine(s.Elapsed);
Console.WriteLine(FastMaths.Sqrt(12345));
}
{
Console.WriteLine(nameof(testSqrt2));
Stopwatch s = Stopwatch.StartNew();
var result = testSqrt2();
s.Stop();
Console.WriteLine(s.Elapsed);
Console.WriteLine(FastMaths.FastSqrt(12345));
}
{
Console.WriteLine(nameof(testSqrt3));
Stopwatch s = Stopwatch.StartNew();
var result = testSqrt3();
s.Stop();
Console.WriteLine(s.Elapsed);
Console.WriteLine(FastMaths.FasterSqrt(12345));
}

return;
}
*/

/*
testIsOdd(0);
testIsOdd(111);
testIsOdd((byte)111);
testIsOdd((short)111);
testIsOdd((long)111);
testIsOdd((float)111);
testIsOdd((double)111);
testIsOdd((UInt128)111);
testIsOdd((decimal)1111);
testIsOdd(111.11);
testIsOdd(111.12);
testIsOdd(112.11);
testIsOdd((decimal)11111);
testIsOdd(111113425625111111111m);
testIsOdd(11111342562511111111.1m);
testIsOdd(11111342562511111111.2m);

testIsOdd2("111");
testIsOdd2("March 19 2013");
testIsOdd2("zero");
testIsOdd2("one");
testIsOdd2("two");
testIsOdd2("three");
testIsOdd2("four");
testIsOdd2("five");
testIsOdd2("six");
testIsOdd2("seven");
testIsOdd2("eight");
testIsOdd2("nine");
testIsOdd2("ten");
testIsOdd2("eleven");
testIsOdd2("twelve");
testIsOdd2("thirteen");
testIsOdd2("fourteen");
testIsOdd2("fifteen");
testIsOdd2("sixteen");
testIsOdd2("seventeen");
testIsOdd2("eighteen");
testIsOdd2("nineteen");
testIsOdd2("twenty");
testIsOdd2("twenty one");
*/

//Console.WriteLine((1.6).Round());
//Console.WriteLine((-1.6).Round());

Console.WriteLine(FastMaths.TwoToThePowerOf(0));
Console.WriteLine(FastMaths.TwoToThePowerOf(1));
Console.WriteLine(FastMaths.TwoToThePowerOf(2));
Console.WriteLine(FastMaths.TwoToThePowerOf(3));

Console.ReadLine();
return;

{
    using Bitmap b = new(256, 512);
    using Graphics g = Graphics.FromImage(b);
    g.Clear(Color.Black);
    for (int x = 0; x < 256; x++)
    {
        int y = Deterministic.Randomish(x, 1);
        g.FillRectangle(Brushes.White, new Rectangle(x, y % 255 + 256, 1, 1));
        for (int i = 2; i < 17; i++)
        {
            Brush brush = (i % 3) switch { 0 => Brushes.Red, 1 => Brushes.Green, _ => Brushes.Blue };

            g.FillRectangle(brush, new Rectangle(x, y % i - 32 + i * 32, 1, 1));
        }
    }
    var fileName = @"d:\temp\Random Test.png";
    b.Save(fileName, ImageFormat.Png);
    OpenWithDefaultProgram(fileName);
}
/*
Console.WriteLine("Rand tests");
{
    int test1 = 456745;
    int result = testRandomOld(test1);
    Console.WriteLine(result);
    int result2 = testRandomNew(test1);
    Console.WriteLine(result2);
    {
        Stopwatch s = Stopwatch.StartNew();
        result = testRandomOld(test1);
        s.Stop();
        Console.WriteLine(s.Elapsed);
    }
    {
        Stopwatch s = Stopwatch.StartNew();
        result = testRandomNew(test1);
        s.Stop();
        Console.WriteLine(s.Elapsed);
    }
}
*/
Console.WriteLine("Int tests");
{
    int test1 = -1;
    int test2 = 3;
    int test3 = -4;
    int test4 = -5;
    int test5 = 6;
    int test6 = -7;
    int test7 = -8;
    int test8 = 9;
    int test9 = -10;
    int result = testIntAbs1(test1, test2, test3, test4, test5, test6, test7, test8, test9);
    Console.WriteLine(result);
    int result2 = testIntAbs2(test1, test2, test3, test4, test5, test6, test7, test8, test9);
    Console.WriteLine(result2);

    {
        Stopwatch s = Stopwatch.StartNew();
        result = testIntAbs1(test1, test2, test3, test4, test5, test6, test7, test8, test9);
        s.Stop();
        Console.WriteLine(s.Elapsed);
    }
    {
        Stopwatch s = Stopwatch.StartNew();
        result = testIntAbs2(test1, test2, test3, test4, test5, test6, test7, test8, test9);
        s.Stop();
        Console.WriteLine(s.Elapsed);
    }
}

Console.ReadKey();
static void OpenWithDefaultProgram(string path)
{
    using Process fileopener = new();
    fileopener.StartInfo.FileName = "explorer";
    fileopener.StartInfo.Arguments = "\"" + path + "\"";
    fileopener.Start();
}

static void testIsOdd<T>(T test1) where T : INumber<T> => Console.WriteLine($"{test1.GetType()} {test1} -> {IsOdd(test1)}: {(IsOdd(test1) == IsOddSensible(test1) ? "Passed" : "Failed")}");
static void testIsOdd2(object test1) => Console.WriteLine($"{test1.GetType()} {test1} -> {IsOdd(test1)}");

static int testRandomNew(int test1)
{
    int result = 0;
    for (long i = 100000000; i >= 0; i--)
    {
        result = Deterministic.Randomish(result, test1);
        result = Deterministic.Randomish(result, test1);
        result = Deterministic.Randomish(result, test1);
        result = Deterministic.Randomish(result, test1);
        result = Deterministic.Randomish(result, test1);
        result = Deterministic.Randomish(result, test1);
        result = Deterministic.Randomish(result, test1);
        result = Deterministic.Randomish(result, test1);
        result = Deterministic.Randomish(result, test1);
        result = Deterministic.Randomish(result, test1);
    }

    return result;
}

static int testIntAbs1(int test1, int test2, int test3, int test4, int test5, int test6, int test7, int test8, int test9)
{
    int result = 0;
    for (long i = 100000000; i >= 0; i--)
    {
        result = Math.Abs(test1);
        result = Math.Abs(test2);
        result = Math.Abs(test3);
        result = Math.Abs(test4);
        result = Math.Abs(test5);
        result = Math.Abs(test6);
        result = Math.Abs(test7);
        result = Math.Abs(test8);
        result = Math.Abs(test9);
    }

    return result;
}

static int testIntAbs2(int test1, int test2, int test3, int test4, int test5, int test6, int test7, int test8, int test9)
{
    int result = 0;
    for (long i = 100000000; i >= 0; i--)
    {
        result = FastMaths.Abs(test1);
        result = FastMaths.Abs(test2);
        result = FastMaths.Abs(test3);
        result = FastMaths.Abs(test4);
        result = FastMaths.Abs(test5);
        result = FastMaths.Abs(test6);
        result = FastMaths.Abs(test7);
        result = FastMaths.Abs(test8);
        result = FastMaths.Abs(test9);
    }

    return result;
}
/*
static int testSqrt1()
{
    int result = 0;
    for (int i = 100000000; i >= 0; i--)
        result = FastMaths.Sqrt(i);

    return result;
}
static int testSqrt2()
{
    int result = 0;
    for (int i = 100000000; i >= 0; i--)
        result = FastMaths.FastSqrt(i);

    return result;
}
static int testSqrt3()
{
    int result = 0;
    for (int i = 100000000; i >= 0; i--)
        result = FastMaths.FasterSqrt(i);

    return result;
}
*/

static bool IsOddSensible<T>(T o) where T : INumber<T> => T.IsOddInteger(o);

static unsafe bool IsOdd(object? o)
{
    start:
    o = o switch
    {
        string s when Int128.TryParse(s, out var n) => n,
        string s when UInt128.TryParse(s, out var n) => n,
        null or string => null,
        byte or sbyte or int or uint or long or ulong or Int128 or UInt128 => o,
        decimal d when d.Scale != 0 => d.ToString(),
        decimal d => new Func<object>(() =>
        {
            SerializationInfo info = new(typeof(decimal), new FormatterConverter());
            ((ISerializable)d).GetObjectData(info, new StreamingContext());
            return info.GetInt32("lo");
        })(),
        _ => o.ToString()
    };
    if (o is string) goto start;
    if (o is null) return false;
    int offset = BitConverter.IsLittleEndian ? 0 : new Func<int>(() =>
    {
        // NB I do not support, encourage, condone or enable middle-endianness.
        // If you find a way to run this on a PDP-11 somehow, you can shove it up your jumper.
        DynamicMethod sizeOf = new("SizeOf", typeof(int), Type.EmptyTypes);
        var generator = sizeOf.GetILGenerator();
        generator.Emit(OpCodes.Sizeof, o.GetType());
        generator.Emit(OpCodes.Ret);
        return (int)sizeOf.Invoke(null, null)! - 1;
    })();
    GCHandle handle = default;
    try { return byte.IsOddInteger(*((byte*)(handle = GCHandle.Alloc(o, GCHandleType.Pinned)).AddrOfPinnedObject() + offset)); }
    finally { if (handle.IsAllocated) handle.Free(); }
}
