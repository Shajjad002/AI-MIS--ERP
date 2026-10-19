using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.MIS.Domain.Entities
{
    public partial class ProgramInfo : BaseEntity
    {
        [Required]
        public string ProgramCode { get; set; }
        public string WorkStationNo { get; set; }

        [DisplayName("Organization")]
        [Required]
        public string OrganizationCode { get; set; }

        [Required]
        public string ProgramName { get; set; }
        public string Description { get; set; }

        [NotMapped]
        public string OrganizationName { get; set; }
    }
}
