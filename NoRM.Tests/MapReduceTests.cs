namespace NoRM.Tests
{
    using System.Linq;
    using Xunit;

    public class MapReduceTests
    {
        private const string _map = "function(){emit(0, this.Price);}";
        private const string _reduce = "function(key, values){var sumPrice = 0;for(var i = 0; i < values.length; ++i){sumPrice += values[i];} return sumPrice;}";

        private class Product
        {
            public ObjectId Id { get; set; }
            public float Price { get; set; }

            public Product()
            {
                Id = ObjectId.NewObjectId();
            }
        }

        public class ProductSum
        {
            public int Id { get; set; }
            public int Value { get; set; }
        }
        public class ProductSumObjectId
        {
            public ObjectId Id { get; set; }
            public int Value { get; set; }
        }

        public MapReduceTests()
        {
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false&strict=false")))
            {
                mongo.Database.DropCollection("Product");
            }
        }
        
        [Fact]
        public void TypedMapReduceOptionSetsCollectionName()
        {
            var options = new MapReduceOptions<Product>();
            Assert.Equal("Product", options.CollectionName);
        }

        [Fact]
        public void MapReduceCreatesACollection()
        {
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                mongo.GetCollection<Product>().Insert(new Product { Price = 1.5f }, new Product { Price = 2.5f }); 
                var mr = mongo.CreateMapReduce();
                var result = mr.Execute(new MapReduceOptions<Product> {Map = _map, Reduce = _reduce});
                var found = false;
                foreach(var c in mongo.Database.GetAllCollections())
                {
                    if (c.Name.EndsWith(result.Result))
                    {
                        found = true;
                        break;
                    }
                }
                Assert.Equal(true, found);
            }
        }

        [Fact]
        public void TemporaryCollectionIsCleanedUpWhenConnectionIsClosed()
        {
            string name;
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                mongo.GetCollection<Product>().Insert(new Product { Price = 1.5f }, new Product { Price = 2.5f }); 
                var mr = mongo.CreateMapReduce();
                name = mr.Execute(new MapReduceOptions<Product> { Map = _map, Reduce = _reduce }).Result;
            }

            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                foreach (var c in mongo.Database.GetAllCollections())
                {
                    Assert.Equal(false, c.Name.EndsWith(name));
                }
            }
        }

        [Fact]
        public void TemporaryCollectionIsCleanedUpWhenDisposed()
        {
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {                
                mongo.GetCollection<Product>().Insert(new Product { Price = 1.5f }, new Product { Price = 2.5f }); 
                string name;
                using (var mr = mongo.CreateMapReduce())
                {
                    name = mr.Execute(new MapReduceOptions<Product> {Map = _map, Reduce = _reduce}).Result;
                }
                foreach (var c in mongo.Database.GetAllCollections())
                {
                    Assert.Equal(false, c.Name.EndsWith(name));
                }
            }
        }

        [Fact]
        public void PermenantCollectionsArentCleanedUp()
        {
            string name;
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                mongo.GetCollection<Product>().Insert(new Product { Price = 1.5f }, new Product { Price = 2.5f });                            
                using (var mr = mongo.CreateMapReduce())
                {
                    name = mr.Execute(new MapReduceOptions<Product> { Map = _map, Reduce = _reduce, Permenant = true}).Result;
                }
            }

            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                var found = false;
                foreach (var c in mongo.Database.GetAllCollections())
                {
                    if (c.Name.EndsWith(name))
                    {
                        found = true;
                        mongo.Database.DropCollection(name);
                    }
                }
                Assert.Equal(true, found);
            }
        }

        [Fact]
        public void CreatesACollectionWithTheSpecifiedOutputName()
        {
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                mongo.GetCollection<Product>().Insert(new Product { Price = 1.5f }, new Product { Price = 2.5f }); 
                using (var mr = mongo.CreateMapReduce())
                {
                    var result = mr.Execute(new MapReduceOptions<Product> {Map = _map, Reduce = _reduce, OutputCollectionName = "TempMr"});
                    Assert.Equal("TempMr", result.Result);
                }
            }
        }

        [Fact]
        public void ActuallydoesAMapAndReduce()
        {
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                mongo.GetCollection<Product>().Insert(new Product { Price = 1.5f }, new Product { Price = 2.5f });
                using (var mr = mongo.CreateMapReduce())
                {
                    var response = mr.Execute(new MapReduceOptions<Product> { Map = _map, Reduce = _reduce});
                    var collection = response.GetCollection<ProductSum>();
                    var r = collection.Find().FirstOrDefault();
                    Assert.Equal(0, r.Id);
                    Assert.Equal(4, r.Value);
                }
            }
        }

        [Fact]
        public void SettingLimitLimitsTheNumberOfResults()
        {
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                mongo.GetCollection<Product>().Insert(new Product { Price = 1.5f }, new Product { Price = 2.5f });
                using (var mr = mongo.CreateMapReduce())
                {
                    var response = mr.Execute(new MapReduceOptions<Product> { Map = "function(){emit(this._id, this.Price);}", Reduce = _reduce, Limit = 1 });
                    var collection = response.GetCollection<ProductSumObjectId>();
                    Assert.Equal(1, collection.Find().Count());
                }
            }
        }

        [Fact]
        public void NotSettingLimitDoesntLimitTheNumberOfResults()
        {
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                mongo.GetCollection<Product>().Insert(new Product { Price = 1.5f }, new Product { Price = 2.5f });
                using (var mr = mongo.CreateMapReduce())
                {
                    var response = mr.Execute(new MapReduceOptions<Product> { Map = "function(){emit(this._id, this.Price);}", Reduce = _reduce});
                    var collection = response.GetCollection<ProductSumObjectId>();
                    Assert.Equal(2, collection.Find().Count());
                }
            }
        }

        [Fact]
        public void FinalizesTheResults()
        {
            using (var mongo = Mongo.ParseConnection(TestHelper.ConnectionString("pooling=false")))
            {
                mongo.GetCollection<Product>().Insert(new Product { Price = 1.5f }, new Product { Price = 2.5f });
                using (var mr = mongo.CreateMapReduce())
                {
                    const string finalize = "function(key, value){return 1;}";
                    var response = mr.Execute(new MapReduceOptions<Product> { Map = _map, Reduce = _reduce, Permenant = true, Finalize = finalize});
                    var collection = response.GetCollection<ProductSum>();
                    var r = collection.Find().FirstOrDefault();
                    Assert.Equal(0, r.Id);
                    Assert.Equal(1, r.Value);
                }
            }
        }
    }
}