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
            string connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING"); // mongo conn string miljøvariabel
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("Auction");
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

        // GET_NEXT functions
        public int GetNextAuctionId()
        {
            var lastAuction = _auctions.AsQueryable().OrderByDescending(a => a.AuctionId).FirstOrDefault();
            return (lastAuction != null) ? lastAuction.AuctionId + 1 : 1;
        }

        public async Task<int> GetNextBidId()
        {
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
                Push(a => a.BidHistory, bid);

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

