from fastapi import FastAPI
import uvicorn
import sys
import argparse
import os

from sentence_transformers import SentenceTransformer
from fastapi import HTTPException
from pydantic import BaseModel
from typing import List
import sentence_transformers_pool 
import logging

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

transformers_cache = os.environ.get('TRANSFORMERS_CACHE')

# Pool dictionary will create a pool of sentence transformers for each model
# we need to manage not too many models at the same time due to memory constraints
# for this example we will not limit the number of models
transformers_dict = {}

app = FastAPI()

class SentenceInput(BaseModel):
    sentences: List[str]
    modelName: str = "all-mpnet-base-v2"

class DimensionsInput(BaseModel):
    modelName: str = "all-mpnet-base-v2" 

class CountTokenInput(BaseModel):
    modelName: str = "all-mpnet-base-v2" 
    text: str 

def load_model(model_name: str) -> SentenceTransformer:
    if model_name not in transformers_dict:
        transformers_dict[model_name] = sentence_transformers_pool.SentenceTransformerPool(model_name=model_name, max_size=3)
    return transformers_dict[model_name].get()

def release_model(model_name: str, model: SentenceTransformer) -> None:
    transformers_dict[model_name].release(model)

@app.get("/ping")
async def ping():
    return "ping"   
    
@app.post("/dimensions")
async def dimensions(input_data: DimensionsInput):
    model = load_model(input_data.modelName)
    try: 
        return  { "model" : input_data.modelName,
                  "maxSequenceLength" : model.max_seq_length,
                  "dimension" : model.get_sentence_embedding_dimension() }
    finally:
        release_model(input_data.modelName, model)

@app.post("/process-sentences")
async def process_sentences(input_data: SentenceInput):
    sentences = input_data.sentences
    model = load_model(input_data.modelName)
    try:
        encoded = model.encode(sentences)
        return {"embeddings": encoded.tolist(), "model" : input_data.modelName}
    finally:
        release_model(input_data.modelName, model)

@app.post("/count-tokens")
async def count_tokens(input_data: CountTokenInput):
    model = load_model(input_data.modelName)
    try:
        tokenizer = model.tokenizer
        tokens = tokenizer.tokenize(input_data.text)
        return { "count" : len(tokens) }
    finally:
        logging.info("Releasing model")
        release_model(input_data.modelName, model)

if __name__ == "__main__":

    # Create an argument parser
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8000, help="Specify the port number")

    # Parse the command-line arguments
    args = parser.parse_args()

    # Get the port number from the parsed arguments
    port = args.port

    if __name__ == "__main__":
        uvicorn.run(app, host="0.0.0.0", port=port)
