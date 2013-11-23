using NUnit.Framework;
using Soundcloud_Playlist_Downloader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Unit_Tests
{
    [TestFixture]
    class SongTests
    {

        private static Song song = null;

        [SetUp]
        public static void setup()
        {
            song = new Song();
        }


        [TestCase("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ~`!@#$%^&*()_+=-{}][\\|'\";:/?.>,,", 
            @"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 and__")]
        public static void Song_Sanitize(string input, string expectedOutput)
        {
            Assert.AreEqual(expectedOutput, song.Sanitize(input));
        }

    }
}
