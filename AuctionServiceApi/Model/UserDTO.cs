using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Model
{
    public class UserDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

        [BsonElement("UserName")]
        public string? UserName { get; set; }

        [BsonElement("UserEmail")]
        public string? UserEmail { get; set; }

        [BsonElement("UserPhone")]
        public int? UserPhone { get; set; }


        public UserDTO(string userName, string userEmail, int userPhone)
        {
            //this.MongoId = mongoId;
            this.UserName = userName;
            this.UserEmail = userEmail;
            this.UserPhone = userPhone;
        }

        public UserDTO()
        {

        }
    }
}

