import logging
import queue
from sentence_transformers import SentenceTransformer
import time

class SentenceTransformerPool:

    @property
    def millisecond_wait_for_allocation(self):
        return self._millisecond_wait_for_allocation

    @millisecond_wait_for_allocation.setter
    def millisecond_wait_for_allocation(self, value: int):
        self._millisecond_wait_for_allocation = value

    def __init__(self, model_name, max_size):
        self.pool = queue.Queue(max_size)
        self.model_name = model_name
        self._millisecond_wait_for_allocation = 0
        # Preallocate the right number of sentences transformers
        for _ in range(max_size):
            self.pool.put(SentenceTransformer(model_name))

    def get(self):
        # Log info level message
        logging.info(f"Getting sentence transformer from the pool. Model name: {self.model_name} remaining: {self.pool.qsize()}")
        # get actual time to calculate how many milliseconds we are waiting for a pool allocation
        start_time = time.time()
        transformer = self.pool.get()
        execution_time = (time.time() - start_time) * 1000
        # add the time we waited for a pool allocation.
        self._millisecond_wait_for_allocation += execution_time
        logging.info(f"Allocated sentence transformer in {execution_time} milliseconds for Model name: {self.model_name}")
        return transformer

    def release(self, transformer):
        self.pool.put(transformer)  # Blocks if necessary until a free slot is available.