using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Zeebe.Client.Bootstrap.Abstractions;
using Zeebe.Client.Api.Worker;
using Microsoft.Extensions.Logging;
using Zeebe.Client.Bootstrap.Options;
using Microsoft.Extensions.Options;
using static Zeebe.Client.Bootstrap.Options.ZeebeClientBootstrapOptions;

namespace Zeebe.Client.Bootstrap
{
    public class ZeebeHostedService : IHostedService, IDisposable
    {
        private readonly IBootstrapJobHandler bootstrapJobHandler;
        private readonly IZeebeClient client;
        private readonly IJobHandlerInfoProvider jobHandlerInfoProvider;
        private readonly WorkerOptions zeebeWorkerOptions;
        private readonly ILogger<ZeebeHostedService> logger;
        private readonly List<IJobWorker> workers = new List<IJobWorker>();

        public ZeebeHostedService(IBootstrapJobHandler bootstrapJobHandler, IZeebeClient client, IJobHandlerInfoProvider jobHandlerInfoProvider, IOptions<ZeebeClientBootstrapOptions> options, ILogger<ZeebeHostedService> logger)
        {
            this.bootstrapJobHandler = bootstrapJobHandler ?? throw new ArgumentNullException(nameof(bootstrapJobHandler));
            this.client = client ?? throw new ArgumentNullException(nameof(client));   
            this.jobHandlerInfoProvider = jobHandlerInfoProvider ?? throw new ArgumentNullException(nameof(jobHandlerInfoProvider));
            this.zeebeWorkerOptions = options?.Value?.Worker ?? throw new ArgumentNullException(nameof(options), $"{nameof(IOptions<ZeebeClientBootstrapOptions>)}.Value.{nameof(ZeebeClientBootstrapOptions.Worker)} is null.");
            ValidateZeebeWorkerOptions(zeebeWorkerOptions);
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));;            
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach(var jobHandlerInfo in jobHandlerInfoProvider.JobHandlerInfoCollection) 
            {
                var worker = client.NewWorker()
                    .JobType(jobHandlerInfo.JobType)                    
                    .Handler((client, job) =>  this.bootstrapJobHandler.HandleJob(job, cancellationToken))                
                    .FetchVariables(jobHandlerInfo.FetchVariabeles)
                    .MaxJobsActive(jobHandlerInfo.MaxJobsActive ?? zeebeWorkerOptions.MaxJobsActive)
                    .Name(zeebeWorkerOptions.Name ?? jobHandlerInfo.WorkerName)
                    .PollingTimeout(jobHandlerInfo.PollingTimeout ?? zeebeWorkerOptions.PollingTimeout)
                    .PollInterval(jobHandlerInfo.PollInterval ?? zeebeWorkerOptions.PollInterval)
                    .Timeout(jobHandlerInfo.Timeout ?? zeebeWorkerOptions.Timeout)
                    .Open();

                logger.LogInformation($"Created job worker to delegate job '{jobHandlerInfo.JobType}' to the boostrap job handler.");

                workers.Add(worker);
            }

            logger.LogInformation($"Created {workers.Count} job workers.");

            return Task.CompletedTask;            
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            workers.ForEach(w => w.Dispose());
            workers.Clear();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            workers.ForEach(w => w.Dispose());
            workers.Clear();
        }

        private static void ValidateZeebeWorkerOptions(WorkerOptions zeebeWorkerOptions)
        {
            if (zeebeWorkerOptions.MaxJobsActive < 1) 
                throw new ArgumentOutOfRangeException($"{nameof(WorkerOptions)}.{nameof(zeebeWorkerOptions.MaxJobsActive)}");
            if(zeebeWorkerOptions.Timeout.TotalMilliseconds < 1)
                throw new ArgumentOutOfRangeException($"{nameof(WorkerOptions)}.{nameof(zeebeWorkerOptions.Timeout)}");
            if(zeebeWorkerOptions.PollInterval.TotalMilliseconds < 1)
                throw new ArgumentOutOfRangeException($"{nameof(WorkerOptions)}.{nameof(zeebeWorkerOptions.PollInterval)}");
            if(zeebeWorkerOptions.PollingTimeout.TotalMilliseconds < 1)
                throw new ArgumentOutOfRangeException($"{nameof(WorkerOptions)}.{nameof(zeebeWorkerOptions.PollingTimeout)}");
            if(String.IsNullOrWhiteSpace(zeebeWorkerOptions.Name) && zeebeWorkerOptions.Name != null)
                throw new ArgumentException($"'{nameof(zeebeWorkerOptions.Name)}' cannot be empty or whitespace.", $"{nameof(WorkerOptions)}.{nameof(zeebeWorkerOptions.Name)}");
        }
    }
}