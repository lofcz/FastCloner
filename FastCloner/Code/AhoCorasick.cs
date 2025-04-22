namespace FastCloner.Code;

public class AhoCorasick
{
    private class Node
    {
        public readonly Dictionary<char, Node> Children = new Dictionary<char, Node>();
        public Node? Failure;
        public bool IsEndOfPattern;
    }

    private readonly Node root = new Node();
    private readonly string[] patterns;

    public AhoCorasick(string[] patterns)
    {
        this.patterns = patterns;
        BuildTrie();
        BuildFailureLinks();
    }

    private void BuildTrie()
    {
        foreach (string pattern in patterns)
        {
            Node current = root;
            foreach (char c in pattern)
            {
                if (!current.Children.ContainsKey(c))
                {
                    current.Children[c] = new Node();
                }
                current = current.Children[c];
            }
            
            current.IsEndOfPattern = true;
        }
    }

    private void BuildFailureLinks()
    {
        Queue<Node> queue = new Queue<Node>();
        
        foreach (Node node in root.Children.Values)
        {
            node.Failure = root;
            queue.Enqueue(node);
        }

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();
            
            foreach ((char character, Node child) in current.Children)
            {
                queue.Enqueue(child);

                Node? failure = current.Failure;
                
                while (failure != null && !failure.Children.ContainsKey(character))
                {
                    failure = failure.Failure;
                }

                child.Failure = failure?.Children.GetValueOrDefault(character) ?? root;
            }
        }
    }

    public bool ContainsAnyPattern(string text)
    {
        Node? current = root;
        
        foreach (char c in text)
        {
            while (current != null && !current.Children.ContainsKey(c))
            {
                current = current.Failure;
            }
            
            current = current?.Children.GetValueOrDefault(c) ?? root;

            if (current.IsEndOfPattern)
            {
                return true;
            }
        }
        return false;
    }
}