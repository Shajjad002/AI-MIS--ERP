using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AI.MIS.Domain.Entities;

public class BaseEntity
{
  public long? Id { get; set; }
  public DateTime SetOn { get; set; }

  [StringLength(20)]
  public string SetBy { get; set; }

  [DisplayName("Active or Not")]
  public bool IsActive { get; set; }

  [DisplayName("Deleted or Not")]
  public bool IsDeleted { get; set; }

  public BaseEntity()
  {
      SetOn = DateTime.Now;
  }
}
