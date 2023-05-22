using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;

namespace Model
{
    public class ArtifactDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

        [BsonElement("ArtifactID")]
        public int ArtifactID { get; set; } //skal måske være en 'int'?

        [BsonElement("ArtifactName")]
        public string? ArtifactName { get; set; }

        [BsonElement("ArtifactDescription")]
        public string? ArtifactDescription { get; set; }

        [BsonElement("CategoryCode")]
        public string? CategoryCode { get; set; }



        [BsonElement("Estimate")]
        public int? Estimate { get; set; }

        [BsonElement("ArtifactPicture")]
        public string? ArtifactPicture { get; set; } // hvordan uploader vi et billede?

        [BsonElement("Status")]
        public string? Status { get; set; } // skal "Pending stå på metode?"


        public ArtifactDTO(int artifactID, string artifactName, string artifactDescription, int estimate, string categoryCode, string artifactOwner)
        {
            this.ArtifactID = artifactID;
            this.ArtifactName = artifactName;
            this.ArtifactDescription = artifactDescription;
            this.CategoryCode = categoryCode;
            this.Estimate = estimate;
        }


        public ArtifactDTO()
        {

        }
    }
}

