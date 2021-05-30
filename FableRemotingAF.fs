namespace FableRemotingAF

module App =

  open System
  open Giraffe
  open Fable.Remoting.Server
  open Fable.Remoting.Giraffe
  open Microsoft.AspNetCore.Http
  open Microsoft.Azure.WebJobs
  open Microsoft.Azure.WebJobs.Extensions.Http
  open System.Threading.Tasks
  open FSharp.Control.Tasks.V2
  open Microsoft.Extensions.Logging
  open SharedTypes

  let getStudents() =
    async {
      return [
          { Name = "Mike";  Age = 23; }
          { Name = "John";  Age = 22; }
          { Name = "Diana"; Age = 22; }
      ]
    }

  let findStudentByName name =
    async {
      let! students = getStudents()
      let student = List.tryFind (fun student -> student.Name = name) students
      return student
    }

  let studentApi : IStudentApi = {
      studentByName = findStudentByName
      allStudents = getStudents
  }

  let webApp : HttpHandler =
    Remoting.createApi()
    |> Remoting.fromValue studentApi
    |> Remoting.withErrorHandler (fun ex routeInfo -> Propagate (sprintf "Message: %s, request body: %A" ex.Message routeInfo.requestBodyText))
    |> Remoting.buildHttpHandler
    
  let errorHandler (ex: exn) (logger: ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> ServerErrors.INTERNAL_ERROR ex.Message

  [<FunctionName "Giraffe">]
  let run ([<HttpTrigger(AuthorizationLevel.Anonymous, Route = "{*any}")>] 
          req: HttpRequest, context: ExecutionContext,
          log: ILogger) =
    
    let hostingEnvironment = req.HttpContext.GetHostingEnvironment()
    hostingEnvironment.ContentRootPath <- context.FunctionAppDirectory

    let func = Some >> Task.FromResult
    { new Microsoft.AspNetCore.Mvc.IActionResult with
        member _.ExecuteResultAsync(ctx) =
          task {
            try
              return! webApp func ctx.HttpContext :> Task
            with exn -> return! errorHandler exn log func ctx.HttpContext :> Task
          } :> Task }
