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
        // This class works as a Data Transfer Object to receive data from the Artifact class in CatalogueService

        [BsonId] // mongo id for a specified ArtifactDTO
        [BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

        [BsonElement("ArtifactID")]
        public int? ArtifactID { get; set; }

        [BsonElement("ArtifactName")]
        public string? ArtifactName { get; set; }

        [BsonElement("ArtifactDescription")]
        public string? ArtifactDescription { get; set; }

        [BsonElement("CategoryCode")]
        public string? CategoryCode { get; set; }

        [BsonElement("ArtifactOwner")] // a user object is set as owner
        public UserDTO? ArtifactOwner { get; set; }

        [BsonElement("Estimate")]
        public int? Estimate { get; set; }

        [BsonElement("ArtifactPicture")]
        public byte[]? ArtifactPicture { get; set; } = null;

        [BsonElement("Status")] // new artifact are initialised as pending and awaiting auction
        public string? Status { get; set; } = "Pending";


        public ArtifactDTO(int artifactID, string artifactName, string artifactDescription, int estimate, string categoryCode)
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