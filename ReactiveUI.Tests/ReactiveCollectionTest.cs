﻿using System.Reactive.Linq;
using Microsoft.Reactive.Testing;
using Xunit;
using System;
using System.Linq;
using System.Collections.Generic;
using ReactiveUI;
using System.IO;
using System.Text;
using ReactiveUI.Testing;
using ReactiveUI.Tests;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace ReactiveUI.Tests
{
    public class ReactiveCollectionTest : IEnableLogger
    {
        [Fact]
        public void CollectionCountChangedTest()
        {
            var fixture = new ReactiveCollection<int>();
            var before_output = new List<int>();
            var output = new List<int>();

            fixture.CollectionCountChanging.Subscribe(before_output.Add);
            fixture.CollectionCountChanged.Subscribe(output.Add);

            fixture.Add(10);
            fixture.Add(20);
            fixture.Add(30);
            fixture.RemoveAt(1);
            fixture.Clear();

            var before_results = new[] {0,1,2,3,2};
            Assert.Equal(before_results.Length, before_output.Count);
            before_results.AssertAreEqual(before_output);

            var results = new[]{1,2,3,2,0};
            Assert.Equal(results.Length, output.Count);
            results.AssertAreEqual(output);
        }

        [Fact]
        public void ItemsAddedAndRemovedTest()
        {
            var fixture = new ReactiveCollection<int>();
            var before_added = new List<int>();
            var before_removed = new List<int>();
            var added = new List<int>();
            var removed = new List<int>();

            fixture.BeforeItemsAdded.Subscribe(before_added.Add);
            fixture.BeforeItemsRemoved.Subscribe(before_removed.Add);
            fixture.ItemsAdded.Subscribe(added.Add);
            fixture.ItemsRemoved.Subscribe(removed.Add);

            fixture.Add(10);
            fixture.Add(20);
            fixture.Add(30);
            fixture.RemoveAt(1);
            fixture.Clear();

            var added_results = new[]{10,20,30};
            Assert.Equal(added_results.Length, added.Count);
            added_results.AssertAreEqual(added);

            var removed_results = new[]{20};
            Assert.Equal(removed_results.Length, removed.Count);
            removed_results.AssertAreEqual(removed);

            Assert.Equal(before_added.Count, added.Count);
            added.AssertAreEqual(before_added);

            Assert.Equal(before_removed.Count, removed.Count);
            removed.AssertAreEqual(before_removed);
        }

        [Fact]
        public void ReactiveCollectionIsRoundTrippable()
        {
            var output = new[] {"Foo", "Bar", "Baz", "Bamf"};
            var fixture = new ReactiveCollection<string>(output);

            string json = JSONHelper.Serialize(fixture);
            var results = JSONHelper.Deserialize<ReactiveCollection<string>>(json);
            this.Log().Info(json);

            output.AssertAreEqual(results);

            bool should_die = true;
            results.ItemsAdded.Subscribe(_ => should_die = false);
            results.Add("Foobar");
            Assert.False(should_die);
        }

        [Fact]
        public void ChangeTrackingShouldFireNotifications()
        {
            var fixture = new ReactiveCollection<TestFixture>() { ChangeTrackingEnabled = true };
            var before_output = new List<Tuple<TestFixture, string>>();
            var output = new List<Tuple<TestFixture, string>>();
            var item1 = new TestFixture() { IsOnlyOneWord = "Foo" };
            var item2 = new TestFixture() { IsOnlyOneWord = "Bar" };

            fixture.ItemChanging.Subscribe(x => {
                before_output.Add(new Tuple<TestFixture,string>((TestFixture)x.Sender, x.PropertyName));
            });

            fixture.ItemChanged.Subscribe(x => {
                output.Add(new Tuple<TestFixture,string>((TestFixture)x.Sender, x.PropertyName));
            });

            fixture.Add(item1);
            fixture.Add(item2);

            item1.IsOnlyOneWord = "Baz";
            Assert.Equal(1, output.Count);
            item2.IsNotNullString = "FooBar";
            Assert.Equal(2, output.Count);

            fixture.Remove(item2);
            item2.IsNotNullString = "FooBarBaz";
            Assert.Equal(2, output.Count);

            fixture.ChangeTrackingEnabled = false;
            item1.IsNotNullString = "Bamf";
            Assert.Equal(2, output.Count);

            new[]{item1, item2}.AssertAreEqual(output.Select(x => x.Item1));
            new[]{item1, item2}.AssertAreEqual(before_output.Select(x => x.Item1));
            new[]{"IsOnlyOneWord", "IsNotNullString"}.AssertAreEqual(output.Select(x => x.Item2));
        }

        [Fact]
        public void ChangeTrackingShouldWorkWhenAddingTheSameThingMoreThanOnce()
        {
            var fixture = new ReactiveCollection<TestFixture>() { ChangeTrackingEnabled = true };
            var output = new List<Tuple<TestFixture, string>>();
            var item1 = new TestFixture() { IsOnlyOneWord = "Foo" };

            fixture.ItemChanged.Subscribe(x => {
                output.Add(new Tuple<TestFixture,string>((TestFixture)x.Sender, x.PropertyName));
            });

            fixture.Add(item1);
            fixture.Add(item1);
            fixture.Add(item1);

            item1.IsOnlyOneWord = "Bar";
            Assert.Equal(1, output.Count);

            fixture.RemoveAt(0);

            item1.IsOnlyOneWord = "Baz";
            Assert.Equal(2, output.Count);

            fixture.RemoveAt(0);
            fixture.RemoveAt(0);

            // We've completely removed item1, we shouldn't be seeing any 
            // notifications from it
            item1.IsOnlyOneWord = "Bamf";
            Assert.Equal(2, output.Count);

            fixture.ChangeTrackingEnabled = false;
            fixture.Add(item1);
            fixture.Add(item1);
            fixture.Add(item1);
            fixture.ChangeTrackingEnabled = true;

            item1.IsOnlyOneWord = "Bonk";
            Assert.Equal(3, output.Count);
        }

        [Fact]
        public void CollectionsShouldntShareSubscriptions()
        {
            var fixture1 = new ReactiveCollection<TestFixture>() { ChangeTrackingEnabled = true };
            var fixture2 = new ReactiveCollection<TestFixture>() { ChangeTrackingEnabled = true };
            var item1 = new TestFixture() { IsOnlyOneWord = "Foo" };
            var output1 = new List<Tuple<TestFixture, string>>();
            var output2 = new List<Tuple<TestFixture, string>>();

            fixture1.ItemChanged.Subscribe(x => {
                output1.Add(new Tuple<TestFixture,string>((TestFixture)x.Sender, x.PropertyName));
            });

            fixture2.ItemChanged.Subscribe(x => {
                output2.Add(new Tuple<TestFixture,string>((TestFixture)x.Sender, x.PropertyName));
            });

            fixture1.Add(item1);
            fixture1.Add(item1);
            fixture2.Add(item1);
            fixture2.Add(item1);

            item1.IsOnlyOneWord = "Bar";
            Assert.Equal(1, output1.Count);
            Assert.Equal(1, output2.Count);

            fixture2.RemoveAt(0);

            item1.IsOnlyOneWord = "Baz";
            Assert.Equal(2, output1.Count);
            Assert.Equal(2, output2.Count);
        }

        [Fact]
        public void CreateCollectionWithoutTimer()
        {
            var input = new[] {"Foo", "Bar", "Baz", "Bamf"};
            var fixture = (new TestScheduler()).With(sched => {
                var f = input.ToObservable(sched).CreateCollection();

                sched.Start();
                return f;
            });
            
            input.AssertAreEqual(fixture);
        }

        [Fact]
        public void CreateCollectionWithTimer()
        {
            var input = new[] {"Foo", "Bar", "Baz", "Bamf"};
            var sched = new TestScheduler();

            ReactiveCollection<string> fixture;
            using (TestUtils.WithScheduler(sched)) {
                fixture = input.ToObservable(sched).CreateCollection(TimeSpan.FromSeconds(0.5));
            }

            sched.RunToMilliseconds(1005);
            fixture.AssertAreEqual(input.Take(2));
            
            sched.RunToMilliseconds(1505);
            fixture.AssertAreEqual(input.Take(3));

            sched.RunToMilliseconds(10000);
            fixture.AssertAreEqual(input);
        }

        [Fact]
        public void DerivedCollectionsShouldFollowBaseCollection()
        {
            var input = new[] {"Foo", "Bar", "Baz", "Bamf"};
            var fixture = new ReactiveCollection<TestFixture>(
                input.Select(x => new TestFixture() { IsOnlyOneWord = x }));

            var output = fixture.CreateDerivedCollection(new Func<TestFixture, string>(x => x.IsOnlyOneWord));

            input.AssertAreEqual(output);

            fixture.Add(new TestFixture() { IsOnlyOneWord = "Hello" });
            Assert.Equal(5, output.Count);
            Assert.Equal(output[4], "Hello");

            fixture.RemoveAt(4);
            Assert.Equal(4, output.Count);

            fixture[1] = new TestFixture() { IsOnlyOneWord = "Goodbye" };
            Assert.Equal(4, output.Count);
            Assert.Equal(output[1], "Goodbye");

            fixture.Clear();
            Assert.Equal(0, output.Count);
        }
    }

#if SILVERLIGHT
    public class JSONHelper
    {
        public static string Serialize<T>(T obj)
        {
            using (var mstream = new MemoryStream()) { 
                var serializer = new DataContractJsonSerializer(obj.GetType());  
                serializer.WriteObject(mstream, obj);  
                mstream.Position = 0;  
  
                using (var sr = new StreamReader(mstream)) {  
                    return sr.ReadToEnd();  
                }  
            }
        }

        public static T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            return (T)serializer.ReadObject(
                new MemoryStream(System.Text.Encoding.Unicode.GetBytes(json)));
        }
    }
#else
    public class JSONHelper
    {
        public static string Serialize<T>(T obj)
        {
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(obj.GetType());
            var ms = new MemoryStream();
            serializer.WriteObject(ms, obj);
            string retVal = Encoding.Default.GetString(ms.ToArray());
            return retVal;
        }

        public static T Deserialize<T>(string json)
        {
            var obj = Activator.CreateInstance<T>();
            var ms = new MemoryStream(Encoding.Unicode.GetBytes(json));
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(obj.GetType());
            obj = (T)serializer.ReadObject(ms);
            ms.Close();
            return obj;
        }
    }
#endif
}