namespace AbsolutePathHelpers.UnitTest;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        AbsolutePath path1 = "C:\\test1\\test2";
        AbsolutePath path2 = "C:\\TEST1\\test2";
        AbsolutePath path3 = "C:\\test1\\test4";

        Assert.Equal(path1, path2);
        Assert.NotEqual(path1, path3);
    }
}