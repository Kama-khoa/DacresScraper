using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseContext.Models
{
    public class Branch
    {
        public int Id { get; set; }
        public string BranchUrl { get; set; }
        [StringLength(150)]
        public string BranchName { get; set; }
        [StringLength(250)]
        public string BranchAddress { get; set; }
        [StringLength(20)]
        public string BranchPostcode { get; set; }
        [StringLength(250)]
        public string? BranchExternalWebsite { get; set; }
        [StringLength(50)]
        public string BranchPhone { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now; 
    }
}
