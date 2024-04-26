# How to integrate python in your Kernel Memory

To use local model from HuggingFace or using any python library to run embedding or Re-Ranker locally it is possible to simply create a python environment and then **run a Ptyhon server with Fast API so you can call python server from C# with simple HttpClient**.

## Use local environment

Create a local environment with python, then allow the ipykernel to create a kernel for jupyter notebooks and manage dependency and python version easily. 

### Create a local environment

This will create a local environment, so you can manage dependencies and python version directly from this folder.

```bash
python3 -m venv KernelMemory
source KernelMemory/bin/activate
# For windows you must use the following command to activate the virtual environment
#  .\KernelMemory\Scripts\activate 
```

You can handle requirements with easy thanks to pip, just install all the package you need **then you can generate a requirements.txt file that contains informations on the package you installed**

```bash
pip install -r requirements.txt
pip freeze > requirements.txt
```

### Using kernel for jupyter notebooks

Then you can create a kernel for jupyter notebooks using the very same environmnent, in this way you can run a notebook with **dependency you need to run your python server**.

This code install the package and then create a kernel called Kernel Memory.

```bash
pip install ipykernel
python -m ipykernel install --user --name=KernelMemory
```

Kernel can be removed from the system if needed with this code.

```bash
jupyter kernelspec remove KernelMemory
```

You can list all kernel installed with this pyton code

```python
import jupyter_client

# Get the list of all available kernels
kernels = jupyter_client.kernelspec.find_kernel_specs()

# Print the list of kernels
for kernel in kernels:
    print(kernel)
```

