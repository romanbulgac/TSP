// Test script to verify frontend TSPLIB upload functionality
console.log('🧪 Testing Frontend TSPLIB Upload Integration');

// Check if we're in the browser context
if (typeof window !== 'undefined') {
    console.log('✅ Running in browser context');
    
    // Test the file upload utilities
    if (typeof window.getFileInput === 'function') {
        console.log('✅ File input utilities available');
    } else {
        console.log('❌ File input utilities not found');
    }
    
    // Test memory management utilities
    if (typeof window.getMemoryInfo === 'function') {
        console.log('✅ Memory management utilities available');
        console.log('Memory status:', window.getMemoryInfo());
    } else {
        console.log('❌ Memory management utilities not found');
    }
    
    // Test if we can access the API base URL
    if (typeof fetch === 'function') {
        console.log('✅ Fetch API available for testing');
        
        // Test a simple API call
        fetch('/api/tsp/health')
            .then(response => {
                if (response.ok) {
                    console.log('✅ API connectivity test passed');
                } else {
                    console.log('❌ API connectivity test failed:', response.status);
                }
            })
            .catch(error => {
                console.log('❌ API connectivity error:', error.message);
            });
    }
    
} else {
    console.log('❌ Not running in browser context - this test should be run in the web application');
}

// Export test functions for browser console use
if (typeof window !== 'undefined') {
    window.testTspLibUpload = async function(fileContent, fileName = 'test.xml') {
        console.log('🧪 Testing TSPLIB file upload with:', fileName);
        
        try {
            const request = {
                fileName: fileName,
                fileContent: fileContent
            };
            
            const response = await fetch('/api/tsp/tsplib/validate', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(request)
            });
            
            if (response.ok) {
                const result = await response.json();
                console.log('✅ Validation successful:', result);
                return result;
            } else {
                const error = await response.text();
                console.log('❌ Validation failed:', error);
                return null;
            }
        } catch (error) {
            console.log('❌ Test error:', error.message);
            return null;
        }
    };
    
    console.log('✅ Test function window.testTspLibUpload() is now available');
    console.log('Usage: window.testTspLibUpload(fileContent, fileName)');
}
