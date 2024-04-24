from fastapi import FastAPI
import uvicorn
import sys
import argparse
import os
from sentence_transformers import CrossEncoder
from fastapi import HTTPException
from pydantic import BaseModel
from typing import List
import cross_encoder_pool 
import logging
from dotenv import load_dotenv, find_dotenv


# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# Pool dictionary will create a pool of sentence transformers for each model
# we need to manage not too many models at the same time due to memory constraints
# for this example we will not limit the number of models
transformers_dict = {}

app = FastAPI()


class RerankInput(BaseModel):
    """
    Represents the input data for the reranking process.

    Attributes:
        question (str): The question to be reranked.
        sentences (List[str]): The list of sentences to be reranked.
        modelName (str, optional): The name of the model to be used for reranking. 
                                   Defaults to "ms-marco-TinyBERT-L-2-v2".
        max_length (int, optional): The maximum length of the input text. Defaults to 512.
    """
    question: str
    sentences: List[str]
    modelName: str = "cross-encoder/ms-marco-TinyBERT-L-2-v2"
    max_length: int = 512

def load_model(model_name: str, max_length: int) -> CrossEncoder:
    if model_name not in transformers_dict:
        token = os.environ.get('HUGGINGFACEHUB_API_TOKEN')
        transformers_dict[model_name] = cross_encoder_pool.CrossEncocderPool(model_name=model_name, max_length=max_length, max_size=3)
    return transformers_dict[model_name].get()

def release_model(model_name: str, model: CrossEncoder) -> None:
    transformers_dict[model_name].release(model)

@app.get("/ping")
async def ping():
    return "ping"   
    
@app.post("/rerank")
async def process_sentences(input_data: RerankInput):
    
    model = load_model(input_data.modelName, input_data.max_length)
    try:
        # Now we need to create an array of tuple using question and all
        # sentences to be reranked
        rerank_data = [(input_data.question, sentence) for sentence in input_data.sentences]
        encoded = model.predict(rerank_data)
        return {"scores": encoded.tolist(), "model" : input_data.modelName}
    finally:
        release_model(input_data.modelName, model)

if __name__ == "__main__":

    # Create an argument parser
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8001, help="Specify the port number")

    _ = load_dotenv(find_dotenv()) # read local .env file

    # Parse the command-line arguments
    args = parser.parse_args()

    # Get the port number from the parsed arguments
    port = args.port

    if __name__ == "__main__":
        uvicorn.run(app, host="0.0.0.0", port=port)
