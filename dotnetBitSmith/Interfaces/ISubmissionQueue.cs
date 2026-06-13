using System;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetBitSmith.Interfaces {
    public interface ISubmissionQueue {
        ValueTask QueueSubmissionAsync(Guid submissionId);
        ValueTask<Guid> DequeueSubmissionAsync(CancellationToken cancellationToken);
    }
}
