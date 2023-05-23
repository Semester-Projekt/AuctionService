using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;



namespace Model
{
	public class Auction
	{
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

		[BsonElement("AuctionId")]
		public int AuctionId { get; set; }
		
		[BsonElement("AuctionEndDate")]
		public DateTime AuctionEndDate { get; set; } = DateTime.Now.AddDays(7);

		[BsonElement("ArtifactID")]
		public int ArtifactID { get; set; }

		[BsonElement("CurrentBid")]
		public BidDTO? CurrentBid { get; set; } = null;

        [BsonElement("FinalBid")]
        public BidDTO? FinalBid { get; set; } = null;

        [BsonElement("BidHistory")]
        public List<BidDTO>? BidHistory { get; set; } = new List<BidDTO>();


        public Auction(int auctionId, int artifactID)
		{
			this.AuctionId = auctionId;
			this.ArtifactID = artifactID;
		}


		public Auction()
		{
			
		}
	}
}

