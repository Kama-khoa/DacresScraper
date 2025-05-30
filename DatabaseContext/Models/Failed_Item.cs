using System;
using System.ComponentModel.DataAnnotations;

namespace DatabaseContext.Models
{
    public class FailedItem
    {
        public int Id { get; set; }
        public int ItemType { get; set; }
        public int? Reference { get; set; }
        public string? Url { get; set; }
        public bool IsItemDeleted { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
