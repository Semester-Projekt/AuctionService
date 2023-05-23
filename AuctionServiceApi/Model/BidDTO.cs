using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;

namespace Model
{
	public class BidDTO
	{
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

		[BsonElement("BidId")]
        public int BidId { get; set; }

        [BsonElement("BidOwner")]
        public int UserId { get; set; }

        [BsonElement("BidAmount")]
        public int BidAmount { get; set; }

		[BsonElement("BidDate")]
		public DateTime BidDate { get; set; } = DateTime.Now;


        public BidDTO()
        {
        }


        public BidDTO(int userId, int bidAmount)
		{
            this.UserId = userId;
            this.BidAmount = bidAmount;
		}
	}
}

