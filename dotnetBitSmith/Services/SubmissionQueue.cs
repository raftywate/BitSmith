using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using dotnetBitSmith.Interfaces;

namespace dotnetBitSmith.Services {
    public class SubmissionQueue : ISubmissionQueue {
        private readonly Channel<Guid> _queue;

        public SubmissionQueue() {
            // Setup a bounded channel to prevent unbounded memory growth if the worker slows down.
            // Bounded to 1000 items, and writers will await if the channel is full.
            var options = new BoundedChannelOptions(1000) {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };
            _queue = Channel.CreateBounded<Guid>(options);
        }

        public async ValueTask QueueSubmissionAsync(Guid submissionId) {
            await _queue.Writer.WriteAsync(submissionId);
        }

        public async ValueTask<Guid> DequeueSubmissionAsync(CancellationToken cancellationToken) {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
