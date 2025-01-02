package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"strings"

	wkhtml "github.com/SebastiaanKlippert/go-wkhtmltopdf"
)

// RequestBody defines the structure of the incoming JSON payload
type RequestBody struct {
	HTML string `json:"html"`
}

// pdfHandler handles POST requests to convert HTML to PDF
func pdfHandler(w http.ResponseWriter, r *http.Request) {
	// Parse the JSON request body
	var body RequestBody
	err := json.NewDecoder(r.Body).Decode(&body)
	if err != nil || body.HTML == "" {
		http.Error(w, "Invalid request payload. Ensure 'html' field is provided.", http.StatusBadRequest)
		return
	}

	// Create a new PDF generator instance
	pdfg, err := wkhtml.NewPDFGenerator()
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to initialize PDF generator: %v", err), http.StatusInternalServerError)
		return
	}

	// Set PDF generation options
	pdfg.Dpi.Set(300)
	pdfg.Orientation.Set(wkhtml.OrientationPortrait)
	pdfg.Grayscale.Set(false)

	// Add HTML content to the PDF generator
	pdfg.AddPage(wkhtml.NewPageReader(strings.NewReader(body.HTML)))

	// Generate the PDF
	err = pdfg.Create()
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to generate PDF: %v", err), http.StatusInternalServerError)
		return
	}

	// Set response headers for PDF download
	w.Header().Set("Content-Type", "application/pdf")
	w.Header().Set("Content-Disposition", "attachment; filename=output.pdf")

	// Write the generated PDF to the response
	_, err = w.Write(pdfg.Bytes())
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to write PDF to response: %v", err), http.StatusInternalServerError)
		return
	}
}

// rootHandler handles GET requests to the root "/" path
func rootHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method == http.MethodGet {
		w.WriteHeader(http.StatusOK)
		_, err := w.Write([]byte("Hello"))
		if err != nil {
			http.Error(w, "Failed to write response", http.StatusInternalServerError)
		}
	} else {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
	}
}

func main() {
	// Register the root and /html-to-pdf handlers
	http.HandleFunc("/", rootHandler)
	http.HandleFunc("/html-to-pdf", pdfHandler)

	port := ":8001"
	fmt.Printf("Starting server on %s...\n", port)
	log.Fatal(http.ListenAndServe(port, nil))
}
