# Setting Up and Running a Uvicorn Application

## 1. Navigate to Your Project Directory
```bash
cd uvicornapp-api
```

## 2. Set Up a Virtual Environment
Create a Python virtual environment to isolate dependencies:

```bash
python3 -m venv .venv
```

Activate the virtual environment:

- **On macOS/Linux:**
  ```bash
  source .venv/bin/activate
  ```
- **On Windows:**
  ```cmd
  .venv\Scripts\activate
  ```

## 3. Install Dependencies
Install the required Python packages:

```bash
pip install uvicorn
```

If you add other dependencies in the future, save them to `requirements.txt`:

```bash
pip freeze > requirements.txt
```

## 4. Run the Application  
Start the app using Uvicorn:

```bash  
uvicorn main:app --reload  
```
- `main` refers to the `main.py` file (without the `.py` extension).  
- `app` refers to the callable ASGI application defined in the `main.py` file.  
- `--reload` automatically restarts the server when you make code changes (useful for development).

## 5. Access the Application  
Open your web browser and go to:  

[http://127.0.0.1:8000](http://127.0.0.1:8000)

You should see this response:
```
Hello, world!
```
