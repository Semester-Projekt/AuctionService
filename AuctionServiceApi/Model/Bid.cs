using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;

namespace Model
{
    public class Bid
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

        [BsonElement("BidId")]
        public int BidId { get; set; }

        [BsonElement("ArtifactId")]
        public int ArtifactId { get; set; }
        
        [BsonElement("BidOwner")]
        public UserDTO? BidOwner { get; set; } = new UserDTO();

        [BsonElement("BidAmount")]
        public int BidAmount { get; set; }

        [BsonElement("BidDate")]
        public DateTime BidDate { get; set; } = DateTime.Now;


        public Bid()
        {

        }

        public Bid(int bidId, int artifactId, UserDTO bidOwner, int bidAmount)
        {
            this.BidId = bidId;
            this.ArtifactId = artifactId;
            this.BidOwner = bidOwner;
            this.BidAmount = bidAmount;
        }
    }
}