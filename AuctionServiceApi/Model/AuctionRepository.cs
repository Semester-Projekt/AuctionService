using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;


namespace Model
{
    public class AuctionRepository
    {
        private readonly IMongoCollection<Auction> _auctions;
        private readonly IMongoCollection<Bid> _bids;


        public AuctionRepository()
        {
            string connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING"); // Retreives a specified Mongo connection string from the Service Deployment file
            var client = new MongoClient(connectionString); // Creates a new instance of the MongoClient class
            var database = client.GetDatabase("Auction"); // Retreives/Creates an existing/new Mongo database
            // Retreive/Creates 2 collections within the Auction database
            _auctions = database.GetCollection<Auction>("Auctions");
            _bids = database.GetCollection<Bid>("Bids");
        }



        //GET
        public async Task<List<Auction>> GetAllAuctions()
        {
            return await _auctions.Aggregate().ToListAsync();
        }

        public async Task<Auction> GetAuctionById(int auctionId)
        {
            // Retrieves an auction by its unique identifier, based on a created filter.
            // This filter method will be used in several other methods within this repository
            var filter = Builders<Auction>.Filter.Eq("AuctionId", auctionId);
            return await _auctions.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<Auction>> GetAuctionsByCategoryCode(string categoryCode)
        {
            var filter = Builders<Auction>.Filter.Eq("CategoryCode", categoryCode);
            return await _auctions.Find(filter).ToListAsync();
        }

        public async Task<List<Bid>> GetAllBids()
        {
            return await _bids.Aggregate().ToListAsync();
        }

        public async Task<int> GetNextBidId()
        {
            // Repository method for retreiving the next BidId when adding a new Bid
            var lastBid = _bids.AsQueryable().OrderByDescending(a => a.BidId).FirstOrDefault();
            return (lastBid != null) ? lastBid.BidId + 1 : 1;
        }






        //POST
        public void AddNewAuction(Auction? auction)
        {
            _auctions.InsertOne(auction!);
        }

        public void AddNewBid(Bid? bid)
        {
            _bids.InsertOne(bid!);
        }






        //PUT
        public async Task UpdateAuction(int auctionId, Auction? auction)
        {
            // .Set is used to set a new value in the specified attributes on a PUT function
            var filter = Builders<Auction>.Filter.Eq(a => a.AuctionId, auctionId);
            var update = Builders<Auction>.Update.
                Set(a => a.AuctionEndDate, auction.AuctionEndDate).
                Set(a => a.FinalBid, auction.FinalBid);

            await _auctions.UpdateOneAsync(filter, update);
        }

        public async Task UpdateAuctionBid(int auctionId, Auction? auction, Bid? bid)
        {
            var filter = Builders<Auction>.Filter.Eq(a => a.AuctionId, auctionId);
            var update = Builders<Auction>.Update.
                Set(a => a.CurrentBid, bid!.BidAmount).
                Push(a => a.BidHistory, bid); // .Push is used to 'push' the specified Bid into the attribute BidHistory. In this instance, a new 
                                              // value is not set, instead a new Bid object is added to the existing values within the BidHistory

            await _auctions.UpdateOneAsync(filter, update);
        }






        //DELETE
        public async Task DeleteAuction(int auctionId)
        {
            var filter = Builders<Auction>.Filter.Eq(a => a.AuctionId, auctionId);
            await _auctions.DeleteOneAsync(filter);
        }
    }
}