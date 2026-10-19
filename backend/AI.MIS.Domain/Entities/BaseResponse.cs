using System;

namespace AI.MIS.Domain.Entities;

 public class BaseResponse
 {
     public bool IsSuccessful { get; set; }
     public bool IsFromReportServer { get; set; }
     public string Message { get; set; }
     public object Data { get; set; }
 }

 public class BaseResponse<T>
 {
     public bool IsSuccessful { get; set; }
     public string Message { get; set; }
     public T Data { get; set; }
 }
