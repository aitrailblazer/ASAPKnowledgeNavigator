from dotenv import load_dotenv
import os
import pandas as pd  # Import the pandas library
from nixtla import NixtlaClient  # Ensure correct import based on your library version
import logging
import matplotlib.pyplot as plt  # For saving the plot

# Set up logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Load environment variables from the .env file
load_dotenv()

# Access the variables
api_key = os.getenv("TIMEGEN_KEY")
base_url = os.getenv("TIMEGEN_ENDPOINT")

# Validate environment variables
if not api_key or not base_url:
    logger.error("TIMEGEN_KEY and TIMEGEN_ENDPOINT must be set in the .env file.")
    raise EnvironmentError("Missing required environment variables.")

# Initialize the Nixtla client
try:
    nixtla_client = NixtlaClient(api_key=api_key, base_url=base_url)
    logger.info("Nixtla client initialized successfully.")
except Exception as e:
    logger.exception("Failed to initialize Nixtla client.")
    raise e

# Read the data with 'timestamp' parsed as datetime
try:
    df = pd.read_csv(
        "https://raw.githubusercontent.com/Nixtla/transfer-learning-time-series/main/datasets/air_passengers.csv",
        parse_dates=["timestamp"]  # Ensure 'timestamp' is parsed as datetime
    )
    logger.info("Data loaded successfully.")
except Exception as e:
    logger.exception("Failed to load data.")
    raise e

# Forecast
try:
    forecast_df = nixtla_client.forecast(
        df=df,
        h=12,
        time_col="timestamp",
        target_col="value",
    )
    logger.info("Forecasting completed successfully.")
except Exception as e:
    logger.exception("Forecasting failed.")
    raise e

# Display forecasted data
print("Forecasted Data:")
print(forecast_df.tail(12))  # Show the last 12 forecasted periods

# Determine the script's directory with fallback
try:
    script_directory = os.path.dirname(os.path.abspath(__file__))
except NameError:
    # Fallback to current working directory if __file__ is not defined
    script_directory = os.getcwd()
    logger.warning("__file__ is not defined. Using current working directory instead.")

# Define filenames
forecast_csv_filename = "forecast_air_passengers.csv"
forecast_plot_filename = "forecast_plot.png"

# Construct absolute paths
forecast_csv_path = os.path.join(script_directory, forecast_csv_filename)
forecast_plot_path = os.path.join(script_directory, forecast_plot_filename)

# Save forecasted data to CSV
try:
    forecast_df.to_csv(forecast_csv_path, index=False)
    print(f"Forecasted data saved to '{forecast_csv_path}'.")
    logger.info(f"Forecasted data saved to '{forecast_csv_path}'.")
except Exception as e:
    logger.exception(f"Failed to save forecasted data to '{forecast_csv_path}'.")
    raise e

# Plot predictions using matplotlib directly
try:
    # Create a new figure
    plt.figure(figsize=(10, 6))
    
    # Plot historical data
    plt.plot(df['timestamp'], df['value'], label='Historical Data')
    
    # Plot forecasted data using the correct column name
    plt.plot(forecast_df['timestamp'], forecast_df['TimeGPT'], label='Forecast', color='orange')
    
    # Plot confidence intervals if available
    if 'lower_bound' in forecast_df.columns and 'upper_bound' in forecast_df.columns:
        plt.fill_between(
            forecast_df['timestamp'],
            forecast_df['lower_bound'],
            forecast_df['upper_bound'],
            color='orange',
            alpha=0.3,
            label='Confidence Interval'
        )
    
    # Add labels and title
    plt.xlabel('Time')
    plt.ylabel('Number of Air Passengers')
    plt.title('Air Passengers Forecast')
    plt.legend()
    
    # Adjust layout
    plt.tight_layout()
    
    # Save the plot to a file in the script's directory
    plt.savefig(forecast_plot_path)
    plt.close()  # Close the figure to free memory
    
    print(f"Forecast plot saved as '{forecast_plot_path}'.")
    logger.info(f"Forecast plot saved as '{forecast_plot_path}'.")
except Exception as e:
    logger.exception("Plotting failed.")
    raise e
