using NUnit.Framework;

namespace Paket.Bootstrapper.Tests
{
    [TestFixture]
    public class WindowsProcessArgumentsParseTests
    {
        private void Verify(string args, params string[] expected)
        {
            var result = WindowsProcessArguments.Parse(args);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Plain_parameter()
        {
            Verify(@"CallMeIshmael", "CallMeIshmael");
        }

        [Test]
        public void Space_in_double_quoted()
        {
            Verify(@"""Call Me Ishmael""", "Call Me Ishmael");
        }

        [Test]
        public void Double_quoted_anywhere()
        {
            Verify(@"Cal""l Me I""shmael", "Call Me Ishmael");
        }

        [Test]
        public void Escape_quotes()
        {
            Verify(@"CallMe\""Ishmael ", @"CallMe""Ishmael");
        }

        [Test]
        public void Escape_backslash_end()
        {
            Verify(@"""Call Me Ishmael\\""", @"Call Me Ishmael\");
        }

        [Test]
        public void Escape_backslash_middle()
        {
            Verify(@"""CallMe\\\""Ishmael""", @"CallMe\""Ishmael");
        }

        [Test]
        public void Backslash_literal_without_quote()
        {
            Verify(@"a\\\b", @"a\\\b");
        }

        [Test]
        public void Backslash_literal_without_quote_in_quoted()
        {
            Verify(@"""a\\\b""", @"a\\\b");
        }

        [Test]
        public void Microsoft_sample_1()
        {
            Verify(@"""a b c""  d  e", "a b c", "d", "e");
        }

        [Test]
        public void Microsoft_sample_2()
        {
            Verify(@"""ab\""c""  ""\\""  d", "ab\"c", "\\", "d");
        }

        [Test]
        public void Microsoft_sample_3()
        {
            Verify(@"a\\\b d""e f""g h", "a\\\\\\b", "de fg", "h");
        }

        [Test]
        public void Microsoft_sample_4()
        {
            Verify(@"a\\\""b c d", @"a\""b", "c", "d");
        }

        [Test]
        public void Microsoft_sample_5()
        {
            Verify(@"a\\\\""b c"" d e", @"a\\b c", "d", "e");
        }

        [Test]
        public void Double_double_quotes_sample_1()
        {
            Verify(@"""a b c""""", @"a b c""");
        }

        [Test]
        public void Double_double_quotes_sample_2()
        {
            Verify(@"""""""CallMeIshmael""""""  b  c ", @"""CallMeIshmael""", "b", "c");
        }

        [Test]
        public void Double_double_quotes_sample_4()
        {
            Verify(@"""""""""Call Me Ishmael"""" b c ", @"""Call", "Me", @"Ishmael", "b", "c");
        }

        [Test]
        public void Triple_double_quotes()
        {
            Verify(@"""""""Call Me Ishmael""""""", @"""Call Me Ishmael""");
        }

        [Test]
        public void Quadruple_double_quotes()
        {
            Verify(@"""""""""Call me Ishmael""""""""", @"""Call", "me", @"Ishmael""");
        }
    }
}
