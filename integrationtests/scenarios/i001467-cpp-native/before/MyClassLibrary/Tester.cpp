#include <iostream>
#include <cpprest/http_client.h>

using namespace web::http;                  // Common HTTP functionality
using namespace web::http::client;          // HTTP client features
using namespace concurrency::streams;       // Asynchronous streams

int main()
{
	// Create http_client to send the request.
	http_client client(U("http://www.bing.com/"));

	// Build request URI and start the request.
	uri_builder builder(U("/search"));
	builder.append_query(U("q"), U("BitTitan"));
	auto request = client.request(methods::GET, builder.to_string()).then([=] (http_response response) {
		 std::cout << response.status_code();
	 });

	request.wait();
	return 0;
}